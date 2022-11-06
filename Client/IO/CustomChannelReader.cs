using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using Client.Impl;

namespace Client.IO;

public class CustomChannelReader<TRead> : ChannelReader<TRead>
{
    private readonly MyProxyClass _myProxy;
    private readonly ChannelReader<TRead> _reader;

    public CustomChannelReader(MyProxyClass myProxy, ChannelReader<TRead> reader)
    {
        _myProxy = myProxy;
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
        await _myProxy.Cancel();
    }
}