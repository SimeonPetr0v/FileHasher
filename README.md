# FileHasher

FileHasher is a parallel duplicate file finder. It scans a folder recursively, computes the SHA-256 hash of every
file using several worker threads at the same time, and groups files with identical content, so you can see which
duplicates waste disk space. It is the final project for the Concurrent Programming course.

## What one job is

**One job = hash one file.** A worker reads the file in 1 MB chunks, feeds them into SHA-256, and stores the
result (path, size, hash, time taken, which worker did it, or the error if the file could not be read).

## Planned features

- **Configurable parallel workers** – e.g. 4 or 8 worker threads hash files at the same time.
- **Producer / consumer** – a scanner thread walks the folder and fills the job queue while the workers already
  start hashing.
- **Start / Stop / Pause / Resume** – one set of controls for all workers together.
- **Live worker view** – for each worker: its state (hashing / waiting / paused / finished), the current file and
  the progress percentage of that file.
- **Queue and results view** – number of pending jobs, a table of processed files and errors, and the duplicate
  groups with the wasted space for each group.
- **Responsive UI** – the UI thread only reads snapshots of the shared data and never does the hashing itself.

## Shared data and how it is protected

| Shared data | Used by | Protection |
|---|---|---|
| `JobQueue` – queue of files to hash | scanner adds, workers take | one `Lock`; "is it empty?" and "dequeue" happen in the same `lock` |
| `ResultStore` – list of results + `hash -> files` map | workers add, UI reads | one `Lock` for both collections, so they always match; UI gets copies |
| `WorkerStatus` – what each worker is doing | its worker writes, UI reads | a small `Lock` per worker; UI reads a snapshot |

### Where race conditions and deadlocks could appear

- **Two workers taking the same job** – if checking `Count > 0` and calling `Dequeue()` were two separate steps.
  Solved by doing both inside one `lock`.
- **Losing a duplicate** – two workers finish files with the same hash, both see "no list for this hash yet" and
  both create one. Solved by locking the whole check-then-add.
- **Torn UI data** – the UI reading a worker's file name from one file and its progress from another. Solved by
  snapshots taken under the worker's lock.
- **Deadlocks** – avoided by never holding two locks at once: each class only locks its own `Lock`, and no code
  calls out to another locked object while holding a lock.
- **Workers waiting forever** – the scanner always calls `CompleteAdding()` in a `finally` block, so workers know
  when the queue is really finished.

## Project structure

```
FileHasher/FileHasher/
├── Program.cs            console UI
├── Core/
│   ├── HashEngine.cs     starts the scanner and worker threads
│   ├── JobQueue.cs       shared job queue
│   ├── ResultStore.cs    shared results and duplicate groups
│   └── WorkerStatus.cs   per-worker live state
└── Models/
    ├── HashJob.cs
    └── HashResult.cs
```

## Running

```bash
dotnet run --project FileHasher/FileHasher -- "C:\path\to\folder" 4
```

Without arguments the app asks for the folder and the number of workers.

## Stages

1. **Core engine** – scanner + N worker threads, shared queue and results protected by locks, duplicate report. ✅
2. **Controls** – Start / Stop / Pause / Resume for all workers together.
3. **Live UI** – live dashboard of the workers, queue, results table and duplicate groups, always responsive.
