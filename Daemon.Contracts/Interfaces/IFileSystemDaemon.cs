using Daemon.Contracts.Payloads.Events;

namespace Daemon.Contracts.Interfaces;

public interface IFileSystemDaemon
{
    Task<CustomChannelReader<FileSystemEvent>> SubscribeChanges(string directory);
}