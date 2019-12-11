using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using theori.Charting;

namespace theori.Database
{
    public sealed class ChartDatabaseWorker : Disposable
    {
        public enum WorkState
        {
            Idle,

            Populating,
            Cleaning,

            //PopulateSearching,
            CleanSearching,
        }

        public WorkState State { get; private set; } = WorkState.Idle;

        private CancellationTokenSource? m_currentTaskCancellation;

        private Task? m_populateSearchTask, m_populateTask;
        private readonly ConcurrentQueue<ChartSetInfo> m_populateQueue = new ConcurrentQueue<ChartSetInfo>();

        private Action? m_onSetToIdleCallback = null;

        private FileSystemWatcher? m_watcher;

        public IEnumerable<ChartSetInfo> ChartSets => ChartDatabaseService.ChartSets;
        public IEnumerable<ChartInfo> Charts => ChartDatabaseService.Charts;

        public string[] CollectionNames => ChartDatabaseService.CollectionNames;
        public void CreateCollection(string collectionName) => ChartDatabaseService.CreateCollection(collectionName);
        public void AddChartToCollection(string collectionName, ChartInfo chart) => ChartDatabaseService.AddToCollection(collectionName, chart);
        public void RemoveChartFromCollection(string collectionName, ChartInfo chart) => ChartDatabaseService.RemoveFromCollection(collectionName, chart);
        public IEnumerable<ChartInfo> GetChartsInCollection(string collectionName) => ChartDatabaseService.GetChartsInCollection(collectionName);

        public ChartDatabaseWorker()
        {
        }

        public void Update()
        {
            switch (State)
            {
                case WorkState.Populating:
                {
                    if (m_populateSearchTask != null && m_populateSearchTask.IsCompleted)
                        m_populateSearchTask = null;

                    if (m_populateTask != null && m_populateTask.IsCompleted)
                        m_populateTask = null;

                    if (m_populateQueue.Count > 0 && m_populateTask == null)
                    {
                        Debug.Assert(m_currentTaskCancellation != null);
                        m_populateTask = Task.Run(() => RunPopulate(m_currentTaskCancellation!.Token));
                    }

                    if (m_populateSearchTask == null && m_populateQueue.Count == 0)
                        SetToIdle();
                } break;

                case WorkState.CleanSearching:
                {
                    foreach (var set in ChartDatabaseService.ChartSets)
                    {
                        string dirPath = Path.Combine(ChartDatabaseService.ChartsDirectory, set.FilePath);
                        if (!Directory.Exists(dirPath))
                            ChartDatabaseService.RemoveSet(set);
                    }

                    Logger.Log("Setting database worker to Clean");
                    State = WorkState.Cleaning;
                }
                break;

                case WorkState.Cleaning:
                {
                    SetToIdle();
                }
                break;
            }
        }

        public void SetToIdle()
        {
            if (State == WorkState.Idle) return;
            Logger.Log("Setting database worker to Idle");

            switch (State)
            {
                case WorkState.CleanSearching:
                    break;

                case WorkState.Populating:
                    m_currentTaskCancellation?.Cancel();
                    m_currentTaskCancellation = null;

                    m_populateTask = m_populateSearchTask = null;
                    break;

                case WorkState.Cleaning:
                    break;
            }

            State = WorkState.Idle;

            m_onSetToIdleCallback?.Invoke();
            m_onSetToIdleCallback = null;
        }

        public void AddRange(IEnumerable<ChartSetInfo> setInfos)
        {
            if (State != WorkState.Idle)
                throw new InvalidOperationException("Database worker is working on another task.");

            Debug.Assert(m_populateTask == null);

            m_currentTaskCancellation = new CancellationTokenSource();

            foreach (var info in setInfos)
                m_populateQueue.Enqueue(info);
            State = WorkState.Populating;
        }

        public void SetToPopulate(Action? onIdle = null)
        {
            if (State == WorkState.Populating)
                return; // already set to clean

            if (State != WorkState.Idle)
                throw new InvalidOperationException("Database worker is working on another task.");

            Logger.Log("Setting database worker to Populate (searching)");

            //ChartDatabaseService.OpenLocal();
            m_onSetToIdleCallback = onIdle;
            State = WorkState.Populating;

            m_currentTaskCancellation = new CancellationTokenSource();
            m_populateSearchTask = Task.Run(() => RunPopulateSearch(m_currentTaskCancellation.Token));
        }

        private void EnqueuePopulateEntry(ChartSetInfo setInfo)
        {
            m_populateQueue!.Enqueue(setInfo);
        }

        private void RunPopulateSearch(CancellationToken ct)
        {
            string chartsDirectory = ChartDatabaseService.ChartsDirectory;
            if (!Directory.Exists(chartsDirectory)) return;

            var setSerializer = new ChartSetSerializer();
            SearchDirectory(chartsDirectory, null);

            void SearchDirectory(string directory, string? currentSubDirectory)
            {
                foreach (string entry in Directory.EnumerateDirectories(directory))
                {
                    if (ct.IsCancellationRequested)
                        ct.ThrowIfCancellationRequested();

                    string entrySubDirectory = currentSubDirectory == null ? Path.GetFileName(entry) : Path.Combine(currentSubDirectory, Path.GetFileName(entry));
                    // TODO(local): check for anything eith any .theori-set extension
                    if (File.Exists(Path.Combine(entry, ".theori-set")))
                    {
                        // TODO(local): see if this can be updated rather than just skipped
                        if (ChartDatabaseService.ContainsSetAtLocation(Path.Combine(entrySubDirectory, ".theori-set"))) continue;
                        EnqueuePopulateEntry(setSerializer.LoadFromFile(chartsDirectory, entrySubDirectory, ".theori-set"));
                    }
                    else SearchDirectory(entry, entrySubDirectory);
                }
            }
        }

        private void RunPopulate(CancellationToken ct)
        {
            while (m_populateQueue!.TryDequeue(out var info))
            {
                if (ct.IsCancellationRequested)
                    ct.ThrowIfCancellationRequested();

                Logger.Log($"Adding { info.FilePath } to the database");
                //if (ChartDatabaseService.ContainsSet(info)) continue;
                ChartDatabaseService.AddSet(info);
            }
        }

        public void SetToClean(Action? onIdle = null)
        {
            if (State == WorkState.Cleaning || State == WorkState.CleanSearching)
                return; // already set to clean

            if (State != WorkState.Idle)
                throw new InvalidOperationException("Database worker is working on another task.");

            Logger.Log("Setting database worker to Clean (searching)");

            //ChartDatabaseService.OpenLocal();
            m_onSetToIdleCallback = onIdle;
            State = WorkState.CleanSearching;
        }

        private static void CleanDatabaseOfMissingEntries()
        {
        }

        #region File System Watcher

        private void WatchForFileSystemChanges()
        {
            if (m_watcher != null) return;

            var watcher = new FileSystemWatcher(ChartDatabaseService.ChartsDirectory)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                Filter = "*.theori-set|*.theori",
            };

            watcher.Changed += Watcher_Changed;
            watcher.Deleted += Watcher_Changed;
            watcher.Created += Watcher_Changed;
            watcher.Renamed += Watcher_Renamed;

            m_watcher = watcher;
            watcher.EnableRaisingEvents = true;
        }

        private void StopWatchingForFileSystemChanges()
        {
            if (m_watcher == null) return;

            m_watcher.Dispose();
            m_watcher = null;
        }

        private void Watcher_Changed(object sender, FileSystemEventArgs e)
        {
        }

        private void Watcher_Renamed(object sender, RenamedEventArgs e)
        {
        }

        #endregion

        private void AddSetFileRelative(string relPath)
        {
            if (PathL.IsFullPath(relPath))
                throw new ArgumentException($"{ nameof(AddSetFileRelative) } expects a relative path.");

            string setDir = Directory.GetParent(relPath).FullName;
            string setFile = Path.GetFileName(relPath);

            Debug.Assert(Path.Combine(setDir, setFile) == relPath);

            var setSerializer = new ChartSetSerializer();
            var setInfo = setSerializer.LoadFromFile(ChartDatabaseService.ChartsDirectory, setDir, setFile);

            ChartDatabaseService.AddSet(setInfo);
        }

        private void AddSetFile(string fullPath)
        {
            if (!PathL.IsFullPath(fullPath))
                throw new ArgumentException($"{ nameof(AddSetFile) } expects a full path and will convert it to a relative path.");

            string relPath;
            try
            {
                relPath = PathL.RelativePath(fullPath, ChartDatabaseService.ChartsDirectory);
            }
            catch (ArgumentException e)
            {
                Logger.Log(e);
                return;
            }

            AddSetFileRelative(relPath);
        }
    }
}
