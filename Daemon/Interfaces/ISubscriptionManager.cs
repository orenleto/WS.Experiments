namespace Daemon.Interfaces;

public interface ISubscriptionManager
{
    /// <summary>
    ///     Subscribe client session to receive changes of directory
    /// </summary>
    /// <param name="clientSession">Subscribing client</param>
    /// <param name="directory">Watching directory path</param>
    void Subscribe(IClientSession clientSession, string directory);

    /// <summary>
    ///     Unsubscribe client from all watching directories
    /// </summary>
    /// <param name="clientSession">Unsubscribing client</param>
    void UnsubscribeAll(IClientSession clientSession);
}