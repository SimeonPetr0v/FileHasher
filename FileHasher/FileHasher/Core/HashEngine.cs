using System.Diagnostics;
using System.Security.Cryptography;
using FileHasher.Models;

namespace FileHasher.Core;

public sealed class HashEngine
{
    private const int BufferSize = 1024 * 1024;

    private readonly WorkerStatus[] _workers;
    private readonly List<Thread> _threads = new();

    public HashEngine(int workerCount)
    {
        if (workerCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(workerCount), "Need at least one worker.");
        }

        _workers = Enumerable.Range(1, workerCount).Select(id => new WorkerStatus(id)).ToArray();
    }

    public JobQueue Queue { get; } = new();

    public ResultStore Results { get; } = new();

    public IReadOnlyList<WorkerStatus> Workers => _workers;

    public bool IsCompleted => _threads.Count > 0 && _threads.All(thread => !thread.IsAlive);

    public void Start(string folder)
    {
        if (_threads.Count > 0)
        {
            throw new InvalidOperationException("The engine was already started.");
        }

        _threads.Add(new Thread(() => Scan(folder)) { IsBackground = true, Name = "Scanner" });

        foreach (var status in _workers)
        {
            _threads.Add(new Thread(() => WorkerLoop(status)) { IsBackground = true, Name = $"Worker {status.Id}" });
        }

        foreach (var thread in _threads)
        {
            thread.Start();
        }
    }

    public void WaitForCompletion()
    {
        foreach (var thread in _threads)
        {
            thread.Join();
        }
    }

    private void Scan(string folder)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };

        try
        {
            foreach (var path in Directory.EnumerateFiles(folder, "*", options))
            {
                long size;
                try
                {
                    size = new FileInfo(path).Length;
                }
                catch (IOException)
                {
                    continue;
                }

                Queue.Add(new HashJob(path, size));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
        finally
        {
            Queue.CompleteAdding();
        }
    }

    private void WorkerLoop(WorkerStatus status)
    {
        var buffer = new byte[BufferSize];

        while (true)
        {
            var job = Queue.TryTake(out bool finished);

            if (job is null)
            {
                if (finished)
                {
                    break;
                }

                status.SetState(WorkerState.Waiting);
                Thread.Sleep(20);
                continue;
            }

            status.BeginFile(job);
            Results.Add(HashFile(job, status, buffer));
            status.EndFile();
        }

        status.SetState(WorkerState.Finished);
    }

    private static HashResult HashFile(HashJob job, WorkerStatus status, byte[] buffer)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var stream = new FileStream(
                job.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, BufferSize, FileOptions.SequentialScan);
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

            long done = 0;
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                sha.AppendData(buffer, 0, read);
                done += read;
                status.ReportProgress(done);
            }

            var hash = Convert.ToHexString(sha.GetHashAndReset());
            return new HashResult(job.Path, job.Size, hash, stopwatch.Elapsed, status.Id, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new HashResult(job.Path, job.Size, null, stopwatch.Elapsed, status.Id, ex.Message);
        }
    }
}
