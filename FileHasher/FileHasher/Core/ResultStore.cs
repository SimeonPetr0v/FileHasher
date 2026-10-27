using FileHasher.Models;

namespace FileHasher.Core;

public sealed class ResultStore
{
    private readonly Lock _lock = new();
    private readonly List<HashResult> _results = new();
    private readonly Dictionary<string, List<HashResult>> _byHash = new();
    private int _errorCount;
    private long _bytesHashed;

    public void Add(HashResult result)
    {
        lock (_lock)
        {
            _results.Add(result);

            if (!result.IsSuccess)
            {
                _errorCount++;
                return;
            }

            _bytesHashed += result.Size;

            if (!_byHash.TryGetValue(result.Hash!, out var sameHash))
            {
                sameHash = new List<HashResult>();
                _byHash[result.Hash!] = sameHash;
            }

            sameHash.Add(result);
        }
    }

    public List<HashResult> GetResults()
    {
        lock (_lock)
        {
            return _results.ToList();
        }
    }

    public List<List<HashResult>> GetDuplicateGroups()
    {
        lock (_lock)
        {
            return _byHash.Values
                .Where(group => group.Count > 1)
                .Select(group => group.ToList())
                .OrderByDescending(group => group[0].Size * (group.Count - 1))
                .ToList();
        }
    }

    public int Count
    {
        get { lock (_lock) return _results.Count; }
    }

    public int ErrorCount
    {
        get { lock (_lock) return _errorCount; }
    }

    public long BytesHashed
    {
        get { lock (_lock) return _bytesHashed; }
    }
}
