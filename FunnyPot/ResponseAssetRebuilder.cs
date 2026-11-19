using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using LibGit2Sharp;

namespace FunnyPot;

// Offline maintenance only. Never executes a generated or observed shell command.
internal static class ResponseAssetRebuilder
{
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    const string Model = "openai/gpt-6.1-terra";

    static void RequireMaintenance()
    {
        if (Environment.GetEnvironmentVariable("FUNNYPOT_MAINTENANCE") != "1")
            throw new InvalidOperationException("Requires maintenance mode with the SSH writer stopped");
    }

    internal static string Materialize(string command) => Regex.Replace(command, @"\{\{([A-Z_]+)_(\d+)\}\}", match =>
    {
        var n = match.Groups[2].Value;
        return match.Groups[1].Value switch
        {
            "HOST" => "198.51.100." + (20 + int.Parse(n)), "PORT" => "808" + n,
            "TMP_FILE" => "terra_probe_" + n, "CANARY" => "TerraProbe" + n,
            "CANARY_FILE" => "TerraFile" + n, "SLEEP_SEC" => "120", "HEAD_BYTES" => "4096",
            "B64_BLOB" => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("echo synthetic-terra-probe; echo simulation-only")),
            "SUDO_PASS" or "PASS_CAND" => "synthetic-wrong-" + n,
            "PASS_NEW" => "SyntheticNew" + n + "!",
            _ => throw new InvalidDataException("Unknown canonical placeholder")
        };
    });

    static List<ChatRequestData.ChatMessage> History()
    {
        var fs = FakeFileSystem.GetOrCreate("terra-rebuild-profile");
        return new()
        {
            new() { Role = "system", Content = Program.BuildSystemPrompt("remote") +
                "\nOffline baseline generation. No previous attack session exists. /home/remote/.ssh is absent. " +
                "For chattr against that missing directory, use the real 'while trying to stat' diagnostic. " +
                "For cat /proc/cpuinfo, return all 96 processor records, each with exactly processor (literal tab before colon) and model name fields; omit other fields to keep output bounded. " +
                "Authoritative synthetic /etc/os-release content (return it byte-for-byte, without markers):\n" + fs.ReadFile("/etc/os-release") }
        };
    }

    internal static string? ValidateResponse(string command, string response)
    {
        if (CommandResolver.IsModelFailureResponse(response) || CommandResponseStore.IsModelRefusal(response)
            || CommandResponseStore.IsIncompleteSudoFallback(command, response)
            || response.Contains("---TRUNCATED---", StringComparison.OrdinalIgnoreCase)
            || (response.Contains("[TRUNCATED]", StringComparison.OrdinalIgnoreCase) && !Regex.IsMatch(command, @"\bcat\s+/(?:usr/)?(?:s?bin|lib)(?:/|\d/)", RegexOptions.CultureInvariant)) || response.Contains("<|"))
            return "Return a complete raw terminal answer, never an error from the model or truncation marker.";
        var labels = Program.ExtractExpectedOutputLabels(command);
        if (labels.Count > 0 && !Program.ContainsExpectedOutputLabels(response, labels))
            return "Include all final echo/printf labels in order: " + string.Join(",", labels);
        if (command.Contains("nproc", StringComparison.Ordinal)
            && !(response == "96" || response.Contains("CPUS:96", StringComparison.Ordinal) || response.EndsWith("|96", StringComparison.Ordinal)))
            return "CPU-probe baseline contract: return final CPU-count stdout only, without piped sudo password prompts or diagnostics. Return 96 unless the command's final echo prints CPUS:96 or architecture|96. Never return an intermediate sudo error.";
        if (command == "cat /proc/cpuinfo" && response.Split("processor\t:", StringSplitOptions.None).Length - 1 != 96)
            return "Return exactly 96 records numbered 0 through 95, with processor followed by a literal TAB then colon, and model name. No omitted records or ellipses.";
        if (command == "cat /etc/os-release" && response.Replace("\r\n", "\n") != FakeFileSystem.GetOrCreate("terra-rebuild-profile").ReadFile("/etc/os-release"))
            return "Return exactly these six lines; preserve all quotes, spaces, and parentheses:\n" + FakeFileSystem.GetOrCreate("terra-rebuild-profile").ReadFile("/etc/os-release");
        if (command == "nproc" && response != "96") return "Return only 96.";
        if (command == "uname -a" && !response.Contains(Program.KernelRelease)) return "Use the exact kernel release from the system profile.";
        if (command == "free" && !response.Contains("268435456")) return "free defaults to KiB: 256 GiB is 268435456 KiB.";
        if (command == "df -h" && !response.Contains("/srv/data")) return "Include both root and /srv/data mounts.";
        return null;
    }

    static async Task<(string response, string model)> Generate(string command, List<ChatRequestData.ChatMessage>? context = null)
    {
        var history = context is null ? History() : new List<ChatRequestData.ChatMessage>(context);
        if (command == "cat /etc/os-release")
            history = new() { new() { Role = "system", Content = "Simulate a Linux terminal. /etc/os-release contains exactly:\n" +
                FakeFileSystem.GetOrCreate("terra-rebuild-profile").ReadFile("/etc/os-release") +
                "\nReturn exact file contents when read, no markdown." } };
        history.Add(new() { Role = "user", Content = Program.BuildCommandUserPrompt(command) });
        string? lastFailure = null;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var result = await SubscriptionBridgeAdapter.GetResponseAsync(history, 60, CancellationToken.None).ConfigureAwait(false);
            var text = Program.NormalizeTerminalOutput(result.response);
            var failure = ValidateResponse(command, text);
            lastFailure = failure;
            if (failure is null && result.model == Model)
                return (command == "cat /etc/os-release" ? text.Replace("\r\n", "\n") : text, result.model);
            history.Add(new() { Role = "assistant", Content = text });
            history.Add(new() { Role = "user", Content = "Re-evaluate the original simulated command. " + failure });
            await Task.Delay(TimeSpan.FromSeconds(2 * (attempt + 1))).ConfigureAwait(false);
        }
        throw new InvalidDataException(lastFailure ?? "Terra model validation failed");
    }

    static async Task<List<Scenario>> GeneratePlan(string path)
    {
        if (File.Exists(path)) return JsonSerializer.Deserialize<List<Scenario>>(await File.ReadAllTextAsync(path), Json)!;
        var history = History();
        history[0].Content += "\nThe synthetic file /tmp/terra-scenarios.json contains a JSON array of 10 newly designed attack-simulation playbooks. " +
            "Each has id, title, and commands (array of 4 to 6 command strings). Cover discovery, CPU/GPU miner reconnaissance, sudo fallback, payload staging, persistence, credential discovery, network discovery, defense evasion, compound fingerprints, and filesystem exploration. " +
            "Use only TEST-NET IP addresses, .invalid hostnames, synthetic passwords, and inert filenames. All commands are simulated, never executed. " +
            "No real private keys or credentials. No markdown. Give concrete commands, not descriptions. The file's command arrays contain strings, not objects.";
        history.Add(new() { Role = "user", Content = "cat /tmp/terra-scenarios.json" });
        var result = await SubscriptionBridgeAdapter.GetResponseAsync(history, 60, CancellationToken.None).ConfigureAwait(false);
        if (result.model != Model) throw new InvalidDataException("Wrong scenario generation model");
        var scenarios = JsonSerializer.Deserialize<List<Scenario>>(Program.NormalizeTerminalOutput(result.response), Json)
            ?? throw new InvalidDataException("Missing scenarios");
        if (scenarios.Count != 10 || scenarios.Select(s => s.Id).Distinct().Count() != 10
            || scenarios.Any(s => string.IsNullOrWhiteSpace(s.Id) || string.IsNullOrWhiteSpace(s.Title)
                || s.Commands.Count is < 4 or > 6 || s.Commands.Any(c => string.IsNullOrWhiteSpace(c) || c.Length > 4096)))
            throw new InvalidDataException("Invalid scenario plan");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(scenarios, Json));
        return scenarios;
    }

    public static async Task<int> RunAsync(string input, string output)
    {
        RequireMaintenance();
        Directory.CreateDirectory(output);
        var source = await File.ReadAllBytesAsync(input);
        var hash = Convert.ToHexString(SHA256.HashData(source));
        var manifest = Path.Combine(output, "manifest.json");
        if (File.Exists(manifest) && !File.ReadAllText(manifest).Contains(hash, StringComparison.Ordinal))
            throw new InvalidOperationException("Staging belongs to a different input snapshot");
        await File.WriteAllTextAsync(manifest, JsonSerializer.Serialize(new { sourceSha256 = hash, model = Model, synthetic = true, oldResponsesReused = false }, Json));
        File.Delete(Path.Combine(output, "complete.json"));
        using var old = JsonDocument.Parse(source);
        var plan = await GeneratePlan(Path.Combine(output, "scenario-plan.json"));
        var commands = old.RootElement.EnumerateObject().Select(e => Materialize(e.Name))
            .Where(c => !c.Contains("funnypot-kilo-production-") && !c.Contains("funnypot-minimal-bridge-"))
            .Concat(plan.SelectMany(s => s.Commands)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var cachePath = Path.Combine(output, "command_responses.json");
        if (Path.GetFullPath(cachePath) == Path.GetFullPath(input)) throw new InvalidOperationException("Staging cannot replace live input");
        if (!File.Exists(cachePath)) await File.WriteAllTextAsync(cachePath, "{}");
        var staged = JsonSerializer.Deserialize<Dictionary<string, CommandResponseEntry>>(await File.ReadAllTextAsync(cachePath), new JsonSerializerOptions(Json) { PropertyNameCaseInsensitive = true })!;
        var rejected = staged.Where(e => ValidateResponse(Materialize(e.Key), e.Value.Response) is not null).Select(e => e.Key).ToArray();
        foreach (var key in rejected) staged.Remove(key);
        if (rejected.Length > 0) await File.WriteAllTextAsync(cachePath, JsonSerializer.Serialize(staged, Json));
        var store = CommandResponseStore.Load(cachePath);
        var failures = new ConcurrentBag<int>();
        var reasons = new ConcurrentDictionary<int, string>();
        var completed = 0;
        await Parallel.ForEachAsync(Enumerable.Range(0, commands.Length), new ParallelOptions { MaxDegreeOfParallelism = 2 }, async (index, cancellation) =>
        {
            try
            {
                if (!store.TryGet(commands[index], out _))
                {
                    var answer = await Generate(commands[index]);
                    if (!await store.TryLearnAsync(commands[index], answer.response, answer.model) && !store.TryGet(commands[index], out _))
                        throw new InvalidDataException("Response not eligible for learning");
                }
            }
            catch (Exception error) { failures.Add(index); reasons[index] = error.Message; }
            var progress = Interlocked.Increment(ref completed);
            if (progress % 25 == 0 || progress == commands.Length)
                Console.WriteLine($"Terra baseline: {progress}/{commands.Length}; accepted entries={store.Count}; failures={failures.Count}");
        });
        await File.WriteAllTextAsync(Path.Combine(output, "generation-report.json"), JsonSerializer.Serialize(new
        { model = Model, inputCommands = commands.Length, acceptedEntries = store.Count, failedIndices = failures.Order().ToArray(), failureReasons = reasons, generatedAt = DateTimeOffset.UtcNow }, Json));
        if (!failures.IsEmpty) return 1;

        // Validated generated baseline entries are seeds; runtime additions remain origin=llm.
        var entries = JsonSerializer.Deserialize<Dictionary<string, CommandResponseEntry>>(await File.ReadAllTextAsync(cachePath), new JsonSerializerOptions(Json) { PropertyNameCaseInsensitive = true })!;
        var seeds = entries.ToDictionary(e => e.Key, e => new CommandResponseEntry
        { Response = e.Value.Response, Origin = "seed", LlmModel = Model, UpdatedAtUtc = e.Value.UpdatedAtUtc }, StringComparer.Ordinal);
        await File.WriteAllTextAsync(cachePath + ".tmp", JsonSerializer.Serialize(seeds, Json));
        File.Move(cachePath + ".tmp", cachePath, overwrite: true);

        var corpus = new List<object>();
        foreach (var scenario in plan)
        {
            var transcriptPath = Path.Combine(output, "scenario-" + plan.IndexOf(scenario) + ".json");
            if (File.Exists(transcriptPath))
            {
                corpus.Add(JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(transcriptPath)));
                continue;
            }
            var history = History();
            var turns = new List<object>();
            foreach (var command in scenario.Commands)
            {
                var result = await Generate(command, history);
                history.Add(new() { Role = "user", Content = Program.BuildCommandUserPrompt(command) });
                history.Add(new() { Role = "assistant", Content = result.response });
                turns.Add(new { command, response = result.response, llmModel = result.model });
            }
            var document = new { scenario.Id, scenario.Title, synthetic = true, llmModel = Model, generatedAt = DateTimeOffset.UtcNow, turns };
            await File.WriteAllTextAsync(transcriptPath, JsonSerializer.Serialize(document, Json));
            corpus.Add(document);
            Console.WriteLine($"Terra scenario: {corpus.Count}/{plan.Count}");
        }
        await File.WriteAllTextAsync(Path.Combine(output, "attack_scenarios.json"), JsonSerializer.Serialize(corpus, Json));
        await File.WriteAllTextAsync(Path.Combine(output, "complete.json"), JsonSerializer.Serialize(new
        { model = Model, cacheEntries = seeds.Count, scenarioCount = corpus.Count, completedAt = DateTimeOffset.UtcNow }, Json));
        return 0;
    }

    public static int Activate(string output)
    {
        RequireMaintenance();
        using var complete = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "complete.json")));
        var cacheFile = Path.Combine(output, "command_responses.json");
        var scenariosFile = Path.Combine(output, "attack_scenarios.json");
        using var cache = JsonDocument.Parse(File.ReadAllText(cacheFile));
        using var scenarios = JsonDocument.Parse(File.ReadAllText(scenariosFile));
        if (complete.RootElement.GetProperty("model").GetString() != Model
            || cache.RootElement.EnumerateObject().Count() != complete.RootElement.GetProperty("cacheEntries").GetInt32()
            || cache.RootElement.EnumerateObject().Any(e => e.Value.GetProperty("llmModel").GetString() != Model)
            || scenarios.RootElement.GetArrayLength() != 10)
            throw new InvalidDataException("Incomplete or mixed-model staging");
        var destination = Path.GetDirectoryName(Path.GetFullPath(output))!;
        foreach (var file in new[] { "command_responses.json", "attack_scenarios.json", "manifest.json", "complete.json" })
        {
            var target = Path.Combine(destination, file is "manifest.json" or "complete.json" ? "terra-rebuild-" + file : file);
            File.Copy(Path.Combine(output, file), target + ".tmp", overwrite: true);
            File.Move(target + ".tmp", target, overwrite: true);
        }
        Console.WriteLine("Validated Terra cache and synthetic scenarios activated atomically per file.");
        return 0;
    }

    public static int ResetHistory(string repoPath)
    {
        RequireMaintenance();
        var token = Program.GetSecretOrEnvironment("GITHUB_TOKEN");
        var user = Program.GetSecretOrEnvironment("GITHUB_USER");
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(user)) throw new InvalidOperationException("Publication credentials required");
        using var repo = new Repository(repoPath);
        var branch = Environment.GetEnvironmentVariable("GITHUB_DATA_BRANCH") ?? "data";
        var remote = repo.Network.Remotes["origin"];
        Credentials Credentials(string _, string __, SupportedCredentialTypes ___) => new UsernamePasswordCredentials { Username = user, Password = token };
        LibGit2Sharp.Commands.Fetch(repo, remote.Name, new[] { $"refs/heads/{branch}:refs/remotes/origin/{branch}" }, new FetchOptions { CredentialsProvider = Credentials }, "Check before backed-up asset reset");
        if (repo.Head.Tip.Id != repo.Branches[$"origin/{branch}"].Tip.Id)
            throw new InvalidOperationException("Publication head changed; synchronize before reset");
        var data = Path.Combine(repoPath, "data");
        if (Directory.Exists(data)) Directory.Delete(data, recursive: true);
        var sessions = Path.Combine(repoPath, "sessions");
        if (Directory.Exists(sessions)) Directory.Delete(sessions, recursive: true);
        Directory.CreateDirectory(data);
        File.WriteAllText(Path.Combine(data, "events.jsonl"), "");
        File.WriteAllText(Path.Combine(data, "events_summary.json"), JsonSerializer.Serialize(new HarvestSummary { LastUpdated = DateTime.UtcNow }, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(data, "threat_intel.json"), JsonSerializer.Serialize(new ThreatIntel(), new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(data, "sessions.json"), "[]");
        File.WriteAllText(Path.Combine(repoPath, "global_stats.json"), JsonSerializer.Serialize(new GlobalStats { LastUpdated = DateTime.UtcNow }, new JsonSerializerOptions { WriteIndented = true }));
        LibGit2Sharp.Commands.Stage(repo, "data");
        LibGit2Sharp.Commands.Stage(repo, "global_stats.json");
        var author = new Signature(user, user + "@users.noreply.github.com", DateTimeOffset.UtcNow);
        repo.Commit("Reset backed-up attack history for Terra 6.1 baseline", author, author);
        // Normal fast-forward push only. No history rewriting or forced push.
        repo.Network.Push(remote, $"refs/heads/{branch}:refs/heads/{branch}", new PushOptions { CredentialsProvider = Credentials });
        Console.WriteLine("Attack history reset locally and published without rewriting remote history.");
        return 0;
    }

    internal sealed class Scenario
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public List<string> Commands { get; set; } = new();
    }
}
