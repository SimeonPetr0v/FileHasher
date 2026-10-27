using FileHasher.Core;

string folder = args.Length > 0 ? args[0] : Ask("Folder to scan: ");
int workerCount = args.Length > 1 && int.TryParse(args[1], out var parsed) ? parsed : AskWorkers();

if (!Directory.Exists(folder))
{
    Console.WriteLine($"Folder not found: {folder}");
    return 1;
}

var engine = new HashEngine(workerCount);
engine.Start(folder);

while (!engine.IsCompleted)
{
    PrintProgress(engine);
    Thread.Sleep(250);
}

engine.WaitForCompletion();
PrintProgress(engine);
Console.WriteLine();
PrintSummary(engine);
return 0;

static string Ask(string prompt)
{
    Console.Write(prompt);
    return (Console.ReadLine() ?? string.Empty).Trim().Trim('"');
}

static int AskWorkers()
{
    var answer = Ask("Number of workers [4]: ");
    return int.TryParse(answer, out var count) && count > 0 ? count : 4;
}

static void PrintProgress(HashEngine engine)
{
    var scanning = engine.Queue.IsAddingCompleted ? "done" : "scanning";
    var line = $"Found {engine.Queue.TotalAdded} ({scanning}) | Hashed {engine.Results.Count} | " +
               $"Queue {engine.Queue.PendingCount} | Errors {engine.Results.ErrorCount}";
    int width = Console.IsOutputRedirected ? 100 : Console.BufferWidth - 1;
    Console.Write($"\r{line.PadRight(width)}");
}

static void PrintSummary(HashEngine engine)
{
    Console.WriteLine();
    Console.WriteLine("Workers:");
    foreach (var worker in engine.Workers.Select(status => status.Snapshot()))
    {
        Console.WriteLine($"  Worker {worker.Id}: {worker.FilesDone} files");
    }

    var groups = engine.Results.GetDuplicateGroups();
    long wasted = groups.Sum(group => group[0].Size * (group.Count - 1));

    Console.WriteLine();
    Console.WriteLine($"Hashed {engine.Results.Count} files ({FormatSize(engine.Results.BytesHashed)}), " +
                      $"{engine.Results.ErrorCount} errors.");
    Console.WriteLine($"Duplicate groups: {groups.Count}, wasted space: {FormatSize(wasted)}");

    foreach (var group in groups.Take(10))
    {
        Console.WriteLine();
        Console.WriteLine($"  {group[0].Hash![..12]}…  {FormatSize(group[0].Size)} x {group.Count}");
        foreach (var result in group)
        {
            Console.WriteLine($"    {result.Path}");
        }
    }

    if (groups.Count > 10)
    {
        Console.WriteLine($"\n  ...and {groups.Count - 10} more groups.");
    }
}

static string FormatSize(long bytes)
{
    string[] units = ["B", "KB", "MB", "GB", "TB"];
    double size = bytes;
    int unit = 0;
    while (size >= 1024 && unit < units.Length - 1)
    {
        size /= 1024;
        unit++;
    }

    return $"{size:0.#} {units[unit]}";
}
