namespace Daemon.Contracts.Payloads;

public enum PayloadType
{
    Exception = -1,
    Success = 1,
    Message = 2,
    Error = 3,
}