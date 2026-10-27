namespace FileHasher.Models;

public sealed class HashJob
{
    public HashJob(string path, long size)
    {
        Path = path;
        Size = size;
    }

    public string Path { get; }

    public long Size { get; }
}
