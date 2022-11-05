using TypeIndicatorConverter.Core.Attribute;

namespace Daemon.Impl.Payloads;

internal class ServerException : Payload
{
    public ServerException(string method, string message)
    {
        Method = method;
        Message = message;
    }
    
    [TypeIndicator] public PayloadType Type => PayloadType.Exception;
    public string Method { get; }
    public string Message { get; }
}