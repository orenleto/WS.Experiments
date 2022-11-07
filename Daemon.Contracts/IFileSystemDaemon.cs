using Daemon.Contracts.Payloads.Events;

namespace Daemon.Contracts;

public interface IFileSystemDaemon
{
    Task<CustomChannelReader<FileSystemEvent>> SubscribeChanges(string directory);
}