using Client.Impl.Payloads;
using Client.IO;

namespace Client.Interfaces;

public interface IFileSystemDaemon
{
    Task<CustomChannelReader<FileSystemEvent>> SubscribeChanges(string directory);
}