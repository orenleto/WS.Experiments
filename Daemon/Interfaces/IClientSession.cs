namespace Daemon.Interfaces;

public interface IClientSession
{
    public Guid Id { get; }
    void Send(EventArgs eventArgs);
}