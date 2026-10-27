using FileHasher.Models;

namespace FileHasher.Core;

public sealed class JobQueue
{
    private readonly Lock _lock = new();
    private readonly Queue<HashJob> _jobs = new();
    private bool _addingCompleted;
    private int _totalAdded;
    private long _totalBytes;

    public void Add(HashJob job)
    {
        lock (_lock)
        {
            _jobs.Enqueue(job);
            _totalAdded++;
            _totalBytes += job.Size;
        }
    }

    public void CompleteAdding()
    {
        lock (_lock)
        {
            _addingCompleted = true;
        }
    }

    public HashJob? TryTake(out bool finished)
    {
        lock (_lock)
        {
            if (_jobs.TryDequeue(out var job))
            {
                finished = false;
                return job;
            }

            finished = _addingCompleted;
            return null;
        }
    }

    public int PendingCount
    {
        get { lock (_lock) return _jobs.Count; }
    }

    public int TotalAdded
    {
        get { lock (_lock) return _totalAdded; }
    }

    public long TotalBytes
    {
        get { lock (_lock) return _totalBytes; }
    }

    public bool IsAddingCompleted
    {
        get { lock (_lock) return _addingCompleted; }
    }
}
