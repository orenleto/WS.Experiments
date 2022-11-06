using Daemon.Impl.Requests;
using FluentResults;
using TypeIndicatorConverter.Core.Attribute;

namespace Daemon.Impl.Payloads;

public class ErrorPayload : Payload
{
    [TypeIndicator] public override PayloadType Type => PayloadType.Error;
    public Request Request { get; set; }
    public string[] Errors { get; set; }
}