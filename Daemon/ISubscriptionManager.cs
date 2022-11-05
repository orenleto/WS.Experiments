namespace Daemon;

public interface ISubscriptionManager
{
    void Subscribe(ClientSession clientSession, string directory);
    void UnsubscribeAll(ClientSession clientSession);
}