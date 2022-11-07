namespace Daemon.Contracts;

public interface ICancelable
{
    Task Cancel();
}