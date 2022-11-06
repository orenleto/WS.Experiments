using Client.Impl.Payloads;
using Client.IO;

namespace Client.Interfaces;

internal interface IFileSystemDaemon
{
    Task<CustomChannelReader<FileSystemEvent>> SubscribeChanges(string path);
}