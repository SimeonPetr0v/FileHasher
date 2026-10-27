using FileHasher.Models;

namespace FileHasher.Core;

public enum WorkerState
{
    Idle,
    Waiting,
    Hashing,
    Finished,
}

public sealed class WorkerSnapshot
{
    public WorkerSnapshot(int id, WorkerState state, string? currentFile, long bytesDone, long bytesTotal, int filesDone)
    {
        Id = id;
        State = state;
        CurrentFile = currentFile;
        BytesDone = bytesDone;
        BytesTotal = bytesTotal;
        FilesDone = filesDone;
    }

    public int Id { get; }

    public WorkerState State { get; }

    public string? CurrentFile { get; }

    public long BytesDone { get; }

    public long BytesTotal { get; }

    public int FilesDone { get; }

    public double Percent => BytesTotal == 0 ? 100 : BytesDone * 100.0 / BytesTotal;
}

public sealed class WorkerStatus
{
    private readonly Lock _lock = new();
    private WorkerState _state = WorkerState.Idle;
    private string? _currentFile;
    private long _bytesDone;
    private long _bytesTotal;
    private int _filesDone;

    public WorkerStatus(int id)
    {
        Id = id;
    }

    public int Id { get; }

    public void BeginFile(HashJob job)
    {
        lock (_lock)
        {
            _state = WorkerState.Hashing;
            _currentFile = job.Path;
            _bytesDone = 0;
            _bytesTotal = job.Size;
        }
    }

    public void ReportProgress(long bytesDone)
    {
        lock (_lock)
        {
            _bytesDone = bytesDone;
        }
    }

    public void EndFile()
    {
        lock (_lock)
        {
            _filesDone++;
            _currentFile = null;
            _state = WorkerState.Idle;
        }
    }

    public void SetState(WorkerState state)
    {
        lock (_lock)
        {
            _state = state;
        }
    }

    public WorkerSnapshot Snapshot()
    {
        lock (_lock)
        {
            return new WorkerSnapshot(Id, _state, _currentFile, _bytesDone, _bytesTotal, _filesDone);
        }
    }
}
