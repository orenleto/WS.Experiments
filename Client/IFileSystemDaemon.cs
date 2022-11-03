namespace Client;

public interface IFileSystemDaemon
{
    Task<CustomChannelReader<FileSystemEvent>> SubscribeChanges(string path);
}