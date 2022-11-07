using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using Client.Impl;
using Client.Impl.Payloads;

namespace Client.IO;

public class CustomChannelReader<TRead> : ChannelReader<TRead> where TRead : Payload
{
    private readonly ProxyInterceptor _proxy;
    private readonly ChannelReader<TRead> _reader;

    public CustomChannelReader(ProxyInterceptor proxy, ChannelReader<TRead> reader)
    {
        _proxy = proxy;
        _reader = reader;
    }

    public override bool TryRead([MaybeNullWhen(false)] out TRead item)
    {
        return _reader.TryRead(out item);
    }

    public override ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken)
    {
        return _reader.WaitToReadAsync(cancellationToken);
    }

    public async void Cancel()
    {
        await _proxy.Cancel();
    }
}