namespace Genesha.Services;

/// <summary>
/// Watches the current workspace's genesha.db (and its -wal/-shm siblings) for changes made outside
/// the currently-loaded pages — e.g. another Genesha window on the same workspace, or an external
/// tool. Raises a debounced <see cref="WorkspaceDataChanged"/> event on a background thread; pages
/// are responsible for marshalling back to their own DispatcherQueue before touching UI.
/// SQLite writes typically fire several FileSystemWatcher events in quick succession (WAL checkpoint,
/// journal, etc.), so events are coalesced with a short debounce rather than reacting to every one.
/// </summary>
public class WorkspaceFileWatcherService : IDisposable
{
    private const int DebounceMilliseconds = 400;

    private readonly CurrentWorkspaceService _currentWorkspace;
    private readonly object _lock = new();
    private FileSystemWatcher? _watcher;
    private Timer? _debounceTimer;

    public event EventHandler? WorkspaceDataChanged;

    public WorkspaceFileWatcherService(CurrentWorkspaceService currentWorkspace)
    {
        _currentWorkspace = currentWorkspace;
        _currentWorkspace.Changed += (_, _) => RestartWatcher();
    }

    private void RestartWatcher()
    {
        lock (_lock)
        {
            _watcher?.Dispose();
            _watcher = null;

            var folderPath = _currentWorkspace.WorkspaceFolderPath;
            if (folderPath is null) return;

            var geneshaDir = Path.Combine(folderPath, ".genesha");
            if (!Directory.Exists(geneshaDir)) return;

            var watcher = new FileSystemWatcher(geneshaDir)
            {
                Filter = "genesha.db*",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            };
            watcher.Changed += OnFileEvent;
            watcher.Created += OnFileEvent;
            watcher.Deleted += OnFileEvent;
            watcher.Renamed += OnFileEvent;
            watcher.EnableRaisingEvents = true;
            _watcher = watcher;
        }
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        lock (_lock)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(
                _ => WorkspaceDataChanged?.Invoke(this, EventArgs.Empty),
                null,
                DebounceMilliseconds,
                Timeout.Infinite);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _watcher?.Dispose();
            _debounceTimer?.Dispose();
        }
    }
}
