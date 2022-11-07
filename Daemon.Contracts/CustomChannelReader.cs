using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using Daemon.Contracts.Payloads;

namespace Daemon.Contracts;

public class CustomChannelReader<TRead> : ChannelReader<TRead>, ICancelable where TRead : Payload
{
    private readonly ICancelable _proxy;
    private readonly ChannelReader<TRead> _reader;

    public CustomChannelReader(ICancelable proxy, ChannelReader<TRead> reader)
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

    public async Task Cancel()
    {
        await _proxy.Cancel();
    }
}