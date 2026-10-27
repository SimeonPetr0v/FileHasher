namespace FileHasher.Models;

public sealed class HashResult
{
    public HashResult(string path, long size, string? hash, TimeSpan elapsed, int workerId, string? error)
    {
        Path = path;
        Size = size;
        Hash = hash;
        Elapsed = elapsed;
        WorkerId = workerId;
        Error = error;
    }

    public string Path { get; }

    public long Size { get; }

    public string? Hash { get; }

    public TimeSpan Elapsed { get; }

    public int WorkerId { get; }

    public string? Error { get; }

    public bool IsSuccess => Error is null;
}
