using System.Text.Json;

namespace Cycles.Core;

public sealed class FileGameStateStore
{
    private readonly string _statePath;
    private readonly Func<GameState> _seedFactory;

    public FileGameStateStore(string statePath, Func<GameState>? seedFactory = null)
    {
        _statePath = Path.GetFullPath(statePath);
        _seedFactory = seedFactory ?? (() => GameSeeder.CreateDefault());
    }

    public string StatePath => _statePath;

    public GameState LoadOrCreate()
    {
        using var stateLock = AcquireLock();
        var state = LoadUnsafe();
        SaveUnsafe(state);
        return state;
    }

    public T Update<T>(Func<GameState, T> update)
    {
        using var stateLock = AcquireLock();
        var state = LoadUnsafe();
        var result = update(state);
        SaveUnsafe(state);
        return result;
    }

    public void Replace(GameState state)
    {
        using var stateLock = AcquireLock();
        SaveUnsafe(state);
    }

    private GameState LoadUnsafe()
    {
        if (!File.Exists(_statePath) || new FileInfo(_statePath).Length == 0)
        {
            return _seedFactory();
        }

        using var stream = File.OpenRead(_statePath);
        return JsonSerializer.Deserialize<GameState>(stream, GameStateJson.Options)
            ?? throw new InvalidOperationException($"State file '{_statePath}' could not be read.");
    }

    private void SaveUnsafe(GameState state)
    {
        var directory = Path.GetDirectoryName(_statePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{_statePath}.tmp";
        using (var stream = File.Create(tempPath))
        {
            JsonSerializer.Serialize(stream, state, GameStateJson.Options);
        }

        File.Move(tempPath, _statePath, overwrite: true);
    }

    private FileStream AcquireLock()
    {
        var directory = Path.GetDirectoryName(_statePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var lockPath = $"{_statePath}.lock";
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (true)
        {
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (DateTimeOffset.UtcNow < deadline)
            {
                Thread.Sleep(50);
            }
        }
    }
}
