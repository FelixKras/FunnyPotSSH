using System.Text.Json;
using System.Text.RegularExpressions;

namespace FunnyPot;

internal sealed class CommandResponseEntry
{
    public string Response { get; init; } = "";
    public string Origin { get; init; } = "seed";
    public string? LlmModel { get; init; }
    public DateTimeOffset? UpdatedAtUtc { get; init; }
}

internal static class CommandCanonicalizer
{
    private const string RealSudoPassword = "Omega#2024!";

    // Order matters: replace compound/longer structures before smaller tokens.
    private static readonly Regex SudoEchoPasswordRegex = new(
        @"(?<=\becho\s+(?:-[neE]+\s+)?)(?<quote>['""])(?<val>[^'""\r\n]{1,64})\k<quote>(?=\s*\|\s*sudo\s+-S\b)",
        RegexOptions.Compiled);

    private static readonly Regex PasswdPrintfThreeRegex = new(
        @"(?<=\bprintf\s+['""]%s\\n%s\\n%s\\n['""]\s+)(?<q1>['""])(?<old>[^'""\r\n]{1,64})\k<q1>\s+(?<q2>['""])(?<new1>[^'""\r\n]{1,64})\k<q2>\s+(?<q3>['""])(?<new2>[^'""\r\n]{1,64})\k<q3>(?=\s*\|\s*(?:sudo\s+|/usr/bin/)?passwd\b)",
        RegexOptions.Compiled);

    private static readonly Regex PasswdPrintfTwoRegex = new(
        @"(?<=\bprintf\s+['""]%s\\n%s\\n['""]\s+)(?<q1>['""])(?<new1>[^'""\r\n]{1,64})\k<q1>\s+(?<q2>['""])(?<new2>[^'""\r\n]{1,64})\k<q2>(?=\s*\|\s*(?:sudo\s+|/usr/bin/)?passwd\b)",
        RegexOptions.Compiled);

    private static readonly Regex UrlEndpointRegex = new(
        @"(?<=(?:https?|ftp|tftp)://)(?<host>(?:\d{1,3}\.){3}\d{1,3}|[a-zA-Z0-9.-]+\.[a-zA-Z]{2,})(?::(?<port>\d{1,5}))?",
        RegexOptions.Compiled);

    private static readonly Regex DevTcpRegex = new(
        @"(?<=/dev/tcp/)(?<host>(?:\d{1,3}\.){3}\d{1,3}|[a-zA-Z0-9.-]+)/(?<port>\d{1,5})\b",
        RegexOptions.Compiled);

    private static readonly Regex TempRandomFileRegex = new(
        @"(?<=(?:/tmp|/var/tmp|/dev/shm)/)(?<name>[A-Za-z0-9_.-]{6,32})\b",
        RegexOptions.Compiled);

    private static readonly Regex CanaryEchoRegex = new(
        @"(?<=\becho\s+)(?<val>[A-Za-z0-9]{8,20})(?=\s*(?:&&|;|\||>|\r|\n|$))",
        RegexOptions.Compiled);

    private static readonly Regex CanaryRedirectRegex = new(
        @"(?<=>\s*)(?<val>[A-Za-z0-9]{8,20})(?=\s*(?:&&|;|\||&|\r|\n|$))",
        RegexOptions.Compiled);

    private static readonly Regex SleepSecondsRegex = new(
        @"(?<=\bsleep\s+)(?<val>\d{2,6})\b",
        RegexOptions.Compiled);

    private static readonly Regex Base64BlobRegex = new(
        @"(?<=\s)(?<val>[A-Za-z0-9+/]{40,}={0,2})(?=\s|[;""'&|)]|$)",
        RegexOptions.Compiled);

    public static bool TryCanonicalize(string command, out string canonicalCommand, out Dictionary<string, string> parameters)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var reverseMap = new Dictionary<string, string>(StringComparer.Ordinal);
        var counters = new Dictionary<string, int>(StringComparer.Ordinal);

        string GetOrAddPlaceholder(string category, string rawValue)
        {
            var key = $"{category}:{rawValue}";
            if (reverseMap.TryGetValue(key, out var existing))
                return existing;

            var index = counters.GetValueOrDefault(category) + 1;
            counters[category] = index;
            var placeholder = $"{{{{{category}_{index}}}}}";
            reverseMap[key] = placeholder;
            map[placeholder] = rawValue;
            return placeholder;
        }

        var working = command;

        working = SudoEchoPasswordRegex.Replace(working, match =>
        {
            var quote = match.Groups["quote"].Value;
            var val = match.Groups["val"].Value;
            if (string.Equals(val, RealSudoPassword, StringComparison.Ordinal))
                return match.Value;
            return $"{quote}{GetOrAddPlaceholder("SUDO_PASS", val)}{quote}";
        });

        working = PasswdPrintfThreeRegex.Replace(working, match =>
        {
            var q1 = match.Groups["q1"].Value;
            var q2 = match.Groups["q2"].Value;
            var q3 = match.Groups["q3"].Value;
            var oldPass = GetOrAddPlaceholder("PASS_CAND", match.Groups["old"].Value);
            var newPass1 = GetOrAddPlaceholder("PASS_NEW", match.Groups["new1"].Value);
            var newPass2 = GetOrAddPlaceholder("PASS_NEW", match.Groups["new2"].Value);
            return $"{q1}{oldPass}{q1} {q2}{newPass1}{q2} {q3}{newPass2}{q3}";
        });

        working = PasswdPrintfTwoRegex.Replace(working, match =>
        {
            var q1 = match.Groups["q1"].Value;
            var q2 = match.Groups["q2"].Value;
            var newPass1 = GetOrAddPlaceholder("PASS_NEW", match.Groups["new1"].Value);
            var newPass2 = GetOrAddPlaceholder("PASS_NEW", match.Groups["new2"].Value);
            return $"{q1}{newPass1}{q1} {q2}{newPass2}{q2}";
        });

        working = UrlEndpointRegex.Replace(working, match =>
        {
            var host = GetOrAddPlaceholder("HOST", match.Groups["host"].Value);
            if (!match.Groups["port"].Success)
                return host;
            var port = GetOrAddPlaceholder("PORT", match.Groups["port"].Value);
            return $"{host}:{port}";
        });

        working = DevTcpRegex.Replace(working, match =>
        {
            var host = GetOrAddPlaceholder("HOST", match.Groups["host"].Value);
            var port = GetOrAddPlaceholder("PORT", match.Groups["port"].Value);
            return $"{host}/{port}";
        });

        working = TempRandomFileRegex.Replace(working, match =>
        {
            var name = match.Groups["name"].Value;
            if (!LooksLikeRandomToken(name))
                return match.Value;
            return GetOrAddPlaceholder("TMP_FILE", name);
        });

        working = CanaryEchoRegex.Replace(working, match =>
        {
            var val = match.Groups["val"].Value;
            if (!LooksLikeRandomToken(val))
                return match.Value;
            return GetOrAddPlaceholder("CANARY", val);
        });

        working = CanaryRedirectRegex.Replace(working, match =>
        {
            var val = match.Groups["val"].Value;
            if (!LooksLikeRandomToken(val))
                return match.Value;
            return GetOrAddPlaceholder("CANARY_FILE", val);
        });

        working = SleepSecondsRegex.Replace(working, match =>
        {
            var val = match.Groups["val"].Value;
            return GetOrAddPlaceholder("SLEEP_SEC", val);
        });

        working = Base64BlobRegex.Replace(working, match =>
        {
            var val = match.Groups["val"].Value;
            return GetOrAddPlaceholder("B64_BLOB", val);
        });

        canonicalCommand = working;
        parameters = map;
        return map.Count > 0 && !string.Equals(command, working, StringComparison.Ordinal);
    }

    public static string TemplatizeResponse(string response, IReadOnlyDictionary<string, string> parameters)
    {
        if (string.IsNullOrEmpty(response) || parameters.Count == 0)
            return response;

        var result = response;
        foreach (var (placeholder, rawValue) in parameters.OrderByDescending(pair => pair.Value.Length))
        {
            if (rawValue.Length >= 3)
                result = result.Replace(rawValue, placeholder, StringComparison.Ordinal);
        }

        return result;
    }

    public static string HydrateResponse(string templateResponse, IReadOnlyDictionary<string, string> parameters)
    {
        if (string.IsNullOrEmpty(templateResponse) || parameters.Count == 0)
            return templateResponse;

        var result = templateResponse;
        foreach (var (placeholder, rawValue) in parameters)
            result = result.Replace(placeholder, rawValue, StringComparison.Ordinal);

        return result;
    }

    private static bool LooksLikeRandomToken(string token)
    {
        if (token.Length < 6)
            return false;
        if (token is "vmware-dragon" or "passwd" or "passwd_link" or "passwd.bak" or "shadow" or "testfile" or "testdir" or "nohup.out")
            return false;

        bool hasDigit = false;
        bool hasUpper = false;
        bool hasLower = false;
        foreach (var c in token)
        {
            if (char.IsDigit(c)) hasDigit = true;
            else if (char.IsUpper(c)) hasUpper = true;
            else if (char.IsLower(c)) hasLower = true;
        }

        return (hasDigit && (hasUpper || hasLower)) || (hasUpper && hasLower && token.Length >= 8);
    }
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
    private Dictionary<string, CommandResponseEntry> _canonicalTemplates;

    private CommandResponseStore(
        string path,
        Dictionary<string, CommandResponseEntry> entries,
        Dictionary<string, CommandResponseEntry> canonicalTemplates)
    {
        _path = path;
        _entries = entries;
        _canonicalTemplates = canonicalTemplates;
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

        var canonicalTemplates = BuildCanonicalTemplateIndex(entries);
        return new CommandResponseStore(path, entries, canonicalTemplates);
    }

    private static Dictionary<string, CommandResponseEntry> BuildCanonicalTemplateIndex(Dictionary<string, CommandResponseEntry> entries)
    {
        var templates = new Dictionary<string, CommandResponseEntry>(StringComparer.Ordinal);
        foreach (var (command, entry) in entries)
        {
            if (!CommandCanonicalizer.TryCanonicalize(command, out var canonical, out var parameters))
                continue;

            var templatized = new CommandResponseEntry
            {
                Response = CommandCanonicalizer.TemplatizeResponse(entry.Response, parameters),
                Origin = entry.Origin,
                LlmModel = entry.LlmModel,
                UpdatedAtUtc = entry.UpdatedAtUtc
            };

            if (!templates.TryGetValue(canonical, out var existing) || entry.Origin == "seed" && existing.Origin != "seed")
                templates[canonical] = templatized;
        }

        return templates;
    }

    public bool TryGet(string command, out CommandResponseEntry entry)
    {
        if (Volatile.Read(ref _entries).TryGetValue(command, out entry!))
            return true;

        if (CommandCanonicalizer.TryCanonicalize(command, out var canonical, out var parameters))
        {
            if (Volatile.Read(ref _entries).TryGetValue(canonical, out var directTemplate))
            {
                entry = new CommandResponseEntry
                {
                    Response = CommandCanonicalizer.HydrateResponse(directTemplate.Response, parameters),
                    Origin = directTemplate.Origin,
                    LlmModel = directTemplate.LlmModel,
                    UpdatedAtUtc = directTemplate.UpdatedAtUtc
                };
                return true;
            }

            if (Volatile.Read(ref _canonicalTemplates).TryGetValue(canonical, out var indexedTemplate))
            {
                entry = new CommandResponseEntry
                {
                    Response = CommandCanonicalizer.HydrateResponse(indexedTemplate.Response, parameters),
                    Origin = indexedTemplate.Origin,
                    LlmModel = indexedTemplate.LlmModel,
                    UpdatedAtUtc = indexedTemplate.UpdatedAtUtc
                };
                return true;
            }
        }

        entry = null!;
        return false;
    }

    public async Task<bool> TryLearnAsync(string command, string response, string llmModel, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(command)
            || string.IsNullOrWhiteSpace(llmModel)
            || IsModelFailure(response)
            || IsIncompleteSudoFallback(command, response)
            || response.Contains("]<]", StringComparison.Ordinal)
            || response.Contains("<|", StringComparison.Ordinal)
            || response.Contains("---TRUNCATED---", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var storeKey = command;
        var storeResponse = response;
        if (CommandCanonicalizer.TryCanonicalize(command, out var canonical, out var parameters))
        {
            storeKey = canonical;
            storeResponse = CommandCanonicalizer.TemplatizeResponse(response, parameters);
        }

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = Volatile.Read(ref _entries);
            if (current.ContainsKey(command) || current.ContainsKey(storeKey))
                return false;

            var newEntry = new CommandResponseEntry
            {
                Response = storeResponse,
                Origin = "llm",
                LlmModel = llmModel,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };

            var next = new Dictionary<string, CommandResponseEntry>(current, StringComparer.Ordinal)
            {
                [storeKey] = newEntry
            };

            await PersistAsync(next, cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _entries, next);
            if (!string.Equals(storeKey, command, StringComparison.Ordinal))
            {
                var nextTemplates = new Dictionary<string, CommandResponseEntry>(Volatile.Read(ref _canonicalTemplates), StringComparer.Ordinal)
                {
                    [storeKey] = newEntry
                };
                Volatile.Write(ref _canonicalTemplates, nextTemplates);
            }
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
            || response.StartsWith("[network error]", StringComparison.OrdinalIgnoreCase)
            || IsModelRefusal(response);
    }

    internal static bool IsModelRefusal(string response)
    {
        var normalized = response.TrimStart().Replace('\u2019', '\'');
        return normalized.StartsWith("Sorry, I can't", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("I'm sorry, but I can't", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("I cannot simulate executing", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsIncompleteSudoFallback(string command, string response)
    {
        return IsSudoNprocFallbackCommand(command) &&
            (response.Contains("sudo: a password is required", StringComparison.OrdinalIgnoreCase) ||
             response.Contains("sudo: 3 incorrect password attempts", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSudoNprocFallbackCommand(string command)
    {
        return command.Contains("sudo -S", StringComparison.Ordinal) &&
            command.Contains("||", StringComparison.Ordinal) &&
            command.Contains("nproc", StringComparison.Ordinal);
    }
}
