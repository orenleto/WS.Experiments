using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Threading.Channels;
using Castle.DynamicProxy;
using Daemon.Contracts.Interfaces;
using Daemon.Contracts.Payloads;

namespace Daemon.Contracts;

public class ProxyInterceptor<T> : IInterceptor, ICancelable, IDisposable where T : Payload
{
    private static readonly ConcurrentDictionary<MethodInfo, Type> _dataTransferTypes = new();
    private readonly Channel<T> _channel;
    private readonly ManualResetEventSlim _initialized = new();
    private readonly IProcessingHandler<T> _processingHandler;

    private readonly CancellationTokenSource _proxyTokenSource = new();
    private readonly ITransport _transport;

    public ProxyInterceptor(
        ITransport transport,
        IProcessingHandler<T> processingHandler
    )
    {
        _transport = transport;
        _processingHandler = processingHandler;
        _channel = Channel.CreateUnbounded<T>();
        _ = Task.Run(async () =>
        {
            try
            {
                await _transport.Connect(_proxyTokenSource.Token);
                _initialized.Set();
                await ProcessingLoopAsync(_proxyTokenSource.Token);
            }
            catch (Exception)
            {
                _proxyTokenSource.Cancel();
            }
        });
    }

    public Task Cancel()
    {
        _proxyTokenSource.Cancel();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _initialized.Dispose();
        _proxyTokenSource.Dispose();
        _transport.Dispose();
    }

    public void Intercept(IInvocation invocation)
    {
        _initialized.Wait(_proxyTokenSource.Token);

        var transferType = _dataTransferTypes.GetOrAdd(invocation.Method, Generator.GenerateDTO);
        var command = Activator.CreateInstance(transferType, invocation.Arguments);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(command);
        invocation.ReturnValue = Callback();

        async Task<CustomChannelReader<T>> Callback()
        {
            await _transport.SendAsync(bytes, _proxyTokenSource.Token);
            return new CustomChannelReader<T>(this, _channel);
        }
    }

    private async Task ProcessingLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var body = await _transport.ReceiveAsync(cancellationToken);
                if (body == ArraySegment<byte>.Empty)
                    break;

                var @event = await _processingHandler.Handle(body, cancellationToken);
                if (@event is not null)
                    await _channel.Writer.WriteAsync(@event, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // normal upon task/token cancellation, disregard
        }
        finally
        {
            _channel.Writer.Complete();
        }
    }
}