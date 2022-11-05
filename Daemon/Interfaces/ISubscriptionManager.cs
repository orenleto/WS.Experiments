using Daemon.Impl;

namespace Daemon.Interfaces;

public interface ISubscriptionManager
{
    void Subscribe(ClientSession clientSession, string directory);
    void UnsubscribeAll(ClientSession clientSession);
}