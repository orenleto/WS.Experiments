﻿using System.Text.Json.Serialization;
using TypeIndicatorConverter.Core.Attribute;

namespace Client;

internal enum PayloadType
{
    Exception = -1,
    Success = 1,
    Message = 2,
    Error = 3,
}

[JsonConverter(typeof(TypeIndicatorConverter.TextJson.TypeIndicatorConverter<Payload>))]
internal abstract class Payload
{
    string Method { get; }
    PayloadType Type { get; }
}

internal class SuccessEvent : Payload
{
    [TypeIndicator] public string Method => "SubscribeChanges-String";
    [TypeIndicator] public PayloadType Type => PayloadType.Success;
    public string Directory { get; set; }
}

internal class FileSystemEventPayload : Payload
{
    [TypeIndicator] public string Method => "SubscribeChanges-String";
    [TypeIndicator] public PayloadType Type => PayloadType.Message;
    public WatcherChangeTypes ChangeType { get; set; }
    public string FullPath { get; set; }
    public string? Name { get; set; }
    public string? OldName { get; set; }
}

internal class ErrorEvent : Payload
{
    [TypeIndicator] public string Method => "SubscribeChanges-String";
    [TypeIndicator] public PayloadType Type => PayloadType.Error;
    public string Directory { get; set; }
    public string Message { get; set; }
}

internal class ServerException : Payload
{
    
    [TypeIndicator] public PayloadType Type => PayloadType.Exception;
    public string Method { get; set; }
    public string Message { get; set; }
}


[JsonConverter(typeof(TypeIndicatorConverter.TextJson.TypeIndicatorConverter<Request>))]
public abstract class Request
{
    public string Method { get; }
}

public class SubscribeChangesRequest : Request
{
    public SubscribeChangesRequest(string directory)
    {
        Directory = directory;
    }

    [TypeIndicator] public string Method => "SubscribeChanges-String";
    public string Directory { get;  }
}