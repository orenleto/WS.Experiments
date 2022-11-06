using TypeIndicatorConverter.Core.Attribute;

namespace Client.Impl.Payloads;

internal class ExceptionPayload : Payload
{
    
    [TypeIndicator] public PayloadType Type => PayloadType.Exception;
    public string Method { get; set; }
    public string Message { get; set; }
}