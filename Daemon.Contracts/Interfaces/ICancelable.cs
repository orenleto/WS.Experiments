namespace Daemon.Contracts.Interfaces;

public interface ICancelable
{
    Task Cancel();
}