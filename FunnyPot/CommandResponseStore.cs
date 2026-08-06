using System.Text.Json;

namespace FunnyPot;

internal sealed class CommandResponseEntry
{
    public string Response { get; init; } = "";
    public string Origin { get; init; } = "seed";
    public string? LlmModel { get; init; }
    public DateTimeOffset? UpdatedAtUtc { get; init; }
}

internal sealed class CommandResponseStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _path;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private Dictionary<string, CommandResponseEntry> _entries;

    private CommandResponseStore(string path, Dictionary<string, CommandResponseEntry> entries)
    {
        _path = path;
        _entries = entries;
    }

    public int Count => Volatile.Read(ref _entries).Count;

    public static CommandResponseStore Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Command response dictionary not found", path);

        var json = File.ReadAllText(path);
        var parsed = JsonSerializer.Deserialize<Dictionary<string, CommandResponseEntry>>(json, JsonOptions)
            ?? throw new InvalidDataException($"Command response dictionary is empty: {path}");
        var entries = new Dictionary<string, CommandResponseEntry>(StringComparer.Ordinal);
        foreach (var (command, entry) in parsed)
        {
            if (string.IsNullOrEmpty(command) || entry is null)
                throw new InvalidDataException($"Command response dictionary contains an invalid entry: {path}");
            entries.Add(command, entry);
        }

        return new CommandResponseStore(path, entries);
    }

    public bool TryGet(string command, out CommandResponseEntry entry)
    {
        return Volatile.Read(ref _entries).TryGetValue(command, out entry!);
    }

    public async Task<bool> TryLearnAsync(string command, string response, string llmModel, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(command)
            || string.IsNullOrWhiteSpace(llmModel)
            || IsModelFailure(response)
            || response.Contains("]<]", StringComparison.Ordinal)
            || response.Contains("<|", StringComparison.Ordinal)
            || response.Contains("---TRUNCATED---", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = Volatile.Read(ref _entries);
            if (current.ContainsKey(command))
                return false;

            var next = new Dictionary<string, CommandResponseEntry>(current, StringComparer.Ordinal)
            {
                [command] = new CommandResponseEntry
                {
                    Response = response,
                    Origin = "llm",
                    LlmModel = llmModel,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                }
            };

            await PersistAsync(next, cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _entries, next);
            return true;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private async Task PersistAsync(Dictionary<string, CommandResponseEntry> entries, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var ordered = entries
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var tempPath = _path + ".tmp";
        await using (var stream = new FileStream(
            tempPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(stream, ordered, JsonOptions, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            stream.Flush(flushToDisk: true);
        }

        File.Move(tempPath, _path, overwrite: true);
    }

    private static bool IsModelFailure(string response)
    {
        return response.StartsWith("[api error]", StringComparison.OrdinalIgnoreCase)
            || response.StartsWith("[network error]", StringComparison.OrdinalIgnoreCase);
    }
}
