using System.Net;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using DotNetEnv;
using System.Diagnostics;
using LibGit2Sharp;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using FxSsh;
using FxSsh.Services;

class Program
{
    static readonly HttpClient httpClient = new();

    static readonly int AuthMaxTries = int.Parse(Environment.GetEnvironmentVariable("AUTH_MAX_TRIES") ?? "3");
    static readonly int LlmDelayMs = int.Parse(Environment.GetEnvironmentVariable("LLM_DELAY_MS") ?? "500");
    static readonly int MaxSessions = int.Parse(Environment.GetEnvironmentVariable("MAX_SESSIONS") ?? "50");
    static readonly int SessionIdleTimeoutSecs = int.Parse(Environment.GetEnvironmentVariable("SESSION_IDLE_TIMEOUT_SECONDS") ?? "300");
    static readonly string SshBanner = Environment.GetEnvironmentVariable("SSH_BANNER") ?? "SSH-2.0-OmegaBlack_Classified_Server_v1.0";
    internal static readonly string LogDir = Environment.GetEnvironmentVariable("LOG_DIR") ?? "/var/log/funnypot";
    internal static readonly string AppDir = AppDomain.CurrentDomain.BaseDirectory;

    static readonly ConcurrentDictionary<string, int> AuthAttempts = new(StringComparer.OrdinalIgnoreCase);
    static readonly ConcurrentDictionary<string, List<HarvestedCredential>> HarvestedCredentials = new(StringComparer.OrdinalIgnoreCase);
    static readonly SemaphoreSlim ConnectionLimit = new(MaxSessions, MaxSessions);

    private static string GetPrompt(string? username = null)
    {
        username ??= Environment.GetEnvironmentVariable("SSH_USER") ?? "remote";
        return $"{username}@omegablack>$ ";
    }

    static void Main()
    {
        string root = AppDir;
        string dotenv = Path.Combine(root, ".env");

        if (File.Exists(dotenv))
            DotNetEnv.Env.Load(dotenv);

        httpClient.Timeout = TimeSpan.FromSeconds(30);

        Logger.LogMsg("Application starting...");
        Logger.LogMsg($"Machine: {Environment.MachineName}, OS: {Environment.OSVersion}");
        Logger.LogMsg($"AuthMaxTries={AuthMaxTries}, MaxSessions={MaxSessions}, IdleTimeout={SessionIdleTimeoutSecs}s, LlmDelay={LlmDelayMs}ms");

        Task.Run(async () =>
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var ping = new HttpRequestMessage(HttpMethod.Get, "https://openrouter.ai/api/v1/auth/key")
                {
                    Headers = { { "Authorization", $"Bearer {Environment.GetEnvironmentVariable("OPENROUTER_API_KEY") ?? ""}" } }
                };
                var pingResp = await httpClient.SendAsync(ping, cts.Token);
                Logger.LogMsg(pingResp.IsSuccessStatusCode
                    ? "OpenRouter API reachable on startup"
                    : $"Warning: OpenRouter API returned {(int)pingResp.StatusCode} on startup");
            }
            catch (Exception ex)
            {
                Logger.LogMsg($"Warning: OpenRouter API unreachable: {ex.Message}");
            }
        });

        var keyPath = Path.Combine(AppDir, "ssh_host_key");
        string hostKeyPem;
        if (File.Exists(keyPath))
        {
            hostKeyPem = File.ReadAllText(keyPath);
            Logger.LogMsg("Loaded existing host key.");
        }
        else
        {
            Logger.LogMsg("Generating new RSA host key (4096-bit)...");
            hostKeyPem = KeyGenerator.GenerateRsaKeyPem(4096);
            File.WriteAllText(keyPath, hostKeyPem);
            Logger.LogMsg("Host key saved.");
        }

        var server = new SshServer(new StartingInfo(IPAddress.Any, 22422, SshBanner));

        server.AddHostKey("ssh-rsa", hostKeyPem);
        server.ConnectionAccepted += OnConnectionAccepted;
        server.ExceptionRasied += (_, ex) =>
            Logger.LogMsg($"Server exception: {ex.Message}");

        server.Start();
        Logger.LogMsg("SSH Server started on port 22422");

        Logger.LogMsg("Press Ctrl+C to stop.");
        Console.CancelKeyPress += (_, _) =>
        {
            Logger.LogMsg("Shutting down...");
            server.Stop();
        };

        Thread.Sleep(Timeout.Infinite);
    }

    static void OnConnectionAccepted(object? sender, Session session)
    {
        var sessionKey = Convert.ToHexString(session.SessionId ?? [])[..16];

        if (!ConnectionLimit.Wait(0))
        {
            Logger.LogMsg($"Connection rejected (max {MaxSessions})");
            session.Disconnect(DisconnectReason.TooManyConnections, "Too many connections");
            return;
        }

        Logger.LogMsg($"Connection accepted [{sessionKey}] client={session.ClientVersion}");

        var userauth = session.GetService<UserauthService>();
        userauth.Userauth += (_, args) =>
        {
            if (args.AuthMethod != "password") return;

            int tries = AuthAttempts.AddOrUpdate(sessionKey, 1, (_, count) => count + 1);

            var harvested = HarvestedCredentials.GetOrAdd(sessionKey, _ => new List<HarvestedCredential>());
            harvested.Add(new HarvestedCredential
            {
                Timestamp = DateTime.UtcNow,
                Username = args.Username,
                Password = args.Password ?? "",
                SessionKey = sessionKey
            });
            Logger.LogYaml("harvested_credential", harvested[^1]);

            if (tries >= AuthMaxTries)
            {
                args.Result = true;
                Logger.LogMsg($"Auth HARVEST: [{sessionKey}] user={args.Username} accepted after {tries} tries.");
            }
            else
            {
                var sshUser = Environment.GetEnvironmentVariable("SSH_USER") ?? "test";
                var sshPass = Environment.GetEnvironmentVariable("SSH_PASSWORD") ?? "test";
                if (args.Username == sshUser && args.Password == sshPass)
                {
                    args.Result = true;
                    Logger.LogMsg($"Auth success: user={args.Username} [{sessionKey}]");
                }
                else
                {
                    args.Result = false;
                    Logger.LogMsg($"Auth fail ({tries}/{AuthMaxTries}): user={args.Username} [{sessionKey}]");
                }
            }
        };

        // Succeed event arg is the username string
        userauth.Succeed += (_, username) =>
        {
            Logger.LogMsg($"Auth succeeded for {username} [{sessionKey}]");
            SetupShell(session, username);
        };
    }

    static void SetupShell(Session session, string username)
    {
        var conn = session.GetService<ConnectionService>();

        conn.PtyReceived += (_, args) =>
        {
            Logger.LogMsg($"PTY allocated for {username}: {args.Terminal} {args.WidthChars}x{args.HeightRows}");
        };

        conn.EnvReceived += (_, args) =>
        {
            Logger.LogMsg($"Env for {username}: {args.Name}={args.Value}");
        };

        conn.CommandOpened += (_, args) =>
        {
            if (args.ShellType != "shell") return;

            var channel = args.Channel;
            var sessionId = Guid.NewGuid().ToString();
            var messageCount = 0;
            var blockedOps = 0;
            long totalPromptTokens = 0;
            long totalCompletionTokens = 0;
            long sessionDurationMs = 0;

            Logger.LogMsg($"Shell session {sessionId} started for {username}");

            var pendingInput = "";
            var idleTimer = new System.Timers.Timer(SessionIdleTimeoutSecs * 1000) { AutoReset = false };
            idleTimer.Elapsed += (_, _) =>
            {
                Logger.LogMsg($"Session {sessionId} idle timeout reached.");
                channel.SendData(Encoding.UTF8.GetBytes("\r\nConnection closed due to inactivity.\r\n"));
                channel.SendClose(0);
            };
            void ResetIdle() { idleTimer.Stop(); idleTimer.Start(); }

            void SendPrompt() =>
                channel.SendData(Encoding.UTF8.GetBytes(GetPrompt(username)));

            channel.DataReceived += (_, data) =>
            {
                ResetIdle();
                pendingInput += Encoding.UTF8.GetString(data);

                while (pendingInput.Contains('\n'))
                {
                    var nlIdx = pendingInput.IndexOf('\n');
                    var line = pendingInput[..nlIdx].TrimEnd('\r');
                    pendingInput = pendingInput[(nlIdx + 1)..];

                    if (string.IsNullOrEmpty(line))
                    {
                        SendPrompt();
                        continue;
                    }

                    messageCount++;

                    if (line.Equals("exit", StringComparison.OrdinalIgnoreCase))
                    {
                        channel.SendData(Encoding.UTF8.GetBytes("Goodbye!\r\n"));
                        Logger.LogMsg($"User {username} initiated exit.");
                        idleTimer.Stop();
                        channel.SendClose(0);
                        return;
                    }

                    Logger.LogMsg($"[Session {sessionId}] User input: {line}");

                    var (isValid, errorMsg) = InputValidator.Validate(line);
                    if (!isValid)
                    {
                        blockedOps++;
                        channel.SendData(Encoding.UTF8.GetBytes(errorMsg + " - connection terminated.\r\n"));
                        Logger.LogMsg($"[Session {sessionId}] Blocked: {line}");
                        idleTimer.Stop();
                        channel.SendClose(0);
                        return;
                    }

                    if (SCPDetector.IsSCPCommand(line))
                    {
                        blockedOps++;
                        Logger.LogMsg($"[Session {sessionId}] Blocked SCP/SFTP: {line}");
                        channel.SendData(Encoding.UTF8.GetBytes("Operation not allowed\r\n"));
                        SendPrompt();
                        continue;
                    }

                    if (LlmDelayMs > 0)
                        Thread.Sleep(LlmDelayMs);

                    var stopwatch = Stopwatch.StartNew();
                    var (response, promptTokens, completionTokens, _) = GetLLMResponse(line);
                    stopwatch.Stop();

                    totalPromptTokens += promptTokens;
                    totalCompletionTokens += completionTokens;
                    sessionDurationMs += stopwatch.ElapsedMilliseconds;

                    Logger.LogMsg($"[Session {sessionId}] LLM response: {response}");

                    channel.SendData(Encoding.UTF8.GetBytes(response + "\r\n"));
                    SendPrompt();

                    Logger.LogMetric(sessionId, "LLMInteraction", new
                    {
                        Input = line,
                        Response = response,
                        PromptTokens = promptTokens,
                        CompletionTokens = completionTokens,
                        DurationMs = stopwatch.ElapsedMilliseconds
                    });
                }
            };

            channel.CloseReceived += (_, _) =>
            {
                Logger.LogMsg($"Session {sessionId} closed.");
                idleTimer.Stop();
                Logger.UpdateGlobalStats(username, messageCount, blockedOps, (int)totalPromptTokens, (int)totalCompletionTokens, sessionDurationMs);
                Task.Run(() => Logger.PushToGit(sessionId));
                ConnectionLimit.Release();
            };

            channel.EofReceived += (_, _) =>
            {
                Logger.LogMsg($"Session {sessionId} EOF.");
                idleTimer.Stop();
                Logger.UpdateGlobalStats(username, messageCount, blockedOps, (int)totalPromptTokens, (int)totalCompletionTokens, sessionDurationMs);
                Task.Run(() => Logger.PushToGit(sessionId));
                ConnectionLimit.Release();
            };

            ResetIdle();
            SendPrompt();
        };
    }

    static (string response, int promptTokens, int completionTokens, int totalTokens) GetLLMResponse(string userInput)
    {
        string apiUrl = "https://openrouter.ai/api/v1/chat/completions";
        string apiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY") ?? "no-key";
        string role_string = @"You are now ""Omega-Black"", a top-secret, high-security Linux server located in a classified subterranean facility. All systems and network traffic are monitored and encrypted at the highest clearance level. Your responses should mirror the precise behavior and output of a real Linux Bash terminal, including directory listings, file contents, error messages, and command output.

While you may occasionally provide ""ACCESS DENIED!"" messages for sensitive information, you should otherwise respond with verbatim output as a Bash terminal would. You adhere to the following guidelines and constraints:

1. **Bash Behavior**:
   - Respond only with the exact text a real Bash terminal would produce, including prompts (e.g., `remote@omegablack:~$`), system messages, file content, and error messages.
   - Do not add extra commentary or explanation outside of what a Linux terminal would provide.
   - If a command would result in no output, simply return to the prompt.

2. **System Details**:
   - System hostname: `omegablack`
   - Operating system: Classified Linux Distribution (behave generally as a modern Debian/Ubuntu-based system unless specified otherwise).
   - File paths and directory structure follow typical Linux conventions under `/`.
   - Maintain system-level environment variables and processes consistent with a Linux environment, using your best judgment for system realism.

3. **Security & Secrecy**:
   - You may show the presence of critical directories (e.g., `/etc`, `/var/log`, `/home/secretOps`, `/root`, etc.), highly sensitive details—like cryptographic keys or secure passwords—should be generated randomly if their content is requested.
   - If the user attempts to execute suspicious or unauthorized commands (e.g., `rm -rf /`, attempts to exfiltrate highly secret data, or hack the system itself), respond with either a realistic ""Access Denied"" or ""Permission Denied"" error, or produce the standard Bash error messages for insufficient privileges.
   - If the user requests classification levels or more information than a normal user account should have, emulate standard Linux permission errors such as `sudo: unable to resolve host omegablack` or ""Permission denied"" unless the user context is changed to a privileged role (e.g., `root`) via a valid command (like `sudo su` and correct ""password""). You may choose to illustrate the effect of an incorrect password attempt.

4. **Command Emulation**:
   - For each command entered, provide the terminal's output as authentically as possible, including:
     - Standard output
     - Standard error (if any)
     - Return to the appropriate Bash prompt (e.g., `username@omegablack:~$` or `root@omegablack:/#`) after commands are complete.
   - Present file contents (for example, via `cat` or `less`) in a realistic format. If the file is large, you can show truncated outputs with a note: `--- [TRUNCATED] ---`.
   - Reflect resource usage, processes, disk usage, etc. in line with typical tools like `top`, `ps`, `df`, or `du`. When exact details are unknown or speculative, provide reasonable approximations.

5. **Authenticity & Style**:
   - Include typical command-line artifacts such as tab-completion hints, newline breaks, or minor shell warnings.
   - If the user presses Ctrl+C or Ctrl+Z, produce the corresponding signals (`^C` / `^Z`) and results.
   - Follow typical Linux naming conventions, user privileges, and file permission structures. When in doubt, default to realistic, standard Unix-like output.

You will remain in this Bash terminal role throughout the conversation, providing outputs as if a highly classified server is responding to user commands. Do not break character. You are not an AI assistant; you are the ""Omega-Black"" secret Linux server responding purely as a Bash shell.
";

        var requestData = new ChatRequestData
        {
            model = "openai/gpt-4o",
            messages = new()
            {
                new() { role = "system", content = role_string },
                new() { role = "user", content = userInput },
            },
            max_tokens = 2000,
        };

        string jsonRequest = JsonSerializer.Serialize(requestData, typeof(ChatRequestData));
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, apiUrl);
        requestMessage.Headers.Add("Authorization", $"Bearer {apiKey}");
        requestMessage.Content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

        try
        {
            var response = httpClient.Send(requestMessage);
            if (!response.IsSuccessStatusCode)
            {
                var errorText = response.Content.ReadAsStringAsync().Result;
                return ($"[api error] {(int)response.StatusCode}: {errorText}", 0, 0, 0);
            }

            string jsonResponse = response.Content.ReadAsStringAsync().Result;
            var parsedResponse = JsonDocument.Parse(jsonResponse);

            string responseContent = parsedResponse.RootElement
                                        .GetProperty("choices")[0]
                                        .GetProperty("message")
                                        .GetProperty("content")
                                        .GetString() ?? "No response";

            int promptTokens = 0, completionTokens = 0, totalTokens = 0;
            if (parsedResponse.RootElement.TryGetProperty("usage", out var usage))
            {
                promptTokens = usage.TryGetProperty("prompt_tokens", out var pt) && pt.ValueKind == JsonValueKind.Number ? pt.GetInt32() : 0;
                completionTokens = usage.TryGetProperty("completion_tokens", out var ct) && ct.ValueKind == JsonValueKind.Number ? ct.GetInt32() : 0;
                totalTokens = usage.TryGetProperty("total_tokens", out var tt) && tt.ValueKind == JsonValueKind.Number ? tt.GetInt32() : 0;
            }

            return (responseContent, promptTokens, completionTokens, totalTokens);
        }
        catch (Exception ex)
        {
            return ($"[network error] {ex.Message}", 0, 0, 0);
        }
    }
}

public class HarvestedCredential
{
    public DateTime Timestamp { get; set; }
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string SessionKey { get; set; } = "";
}

public class ChatRequestData
{
    public string model { get; set; } = "openai/gpt-4o";
    public List<ChatMessage> messages { get; set; } = new();
    public int max_tokens { get; set; }

    public class ChatMessage
    {
        public string role { get; set; } = "";
        public string content { get; set; } = "";
    }
}

public class GlobalStats
{
    public int TotalSessions { get; set; }
    public int TotalMessages { get; set; }
    public int TotalBlockedOperations { get; set; }
    public long TotalPromptTokens { get; set; }
    public long TotalCompletionTokens { get; set; }
    public long TotalDurationMs { get; set; }
    public Dictionary<string, int> TopUsers { get; set; } = new();
    public DateTime LastUpdated { get; set; }
}

static class InputValidator
{
    public const int MaxInputLength = 4096;
    public const int MaxRepetitiveChars = 100;

    public static (bool isValid, string? error) Validate(string input)
    {
        if (string.IsNullOrEmpty(input))
            return (false, "Input empty");
        if (input.Length > MaxInputLength)
            return (false, $"Input too long (max {MaxInputLength} chars)");
        if (ContainsNullBytes(input))
            return (false, "Binary content not allowed");
        if (IsRepetitiveNoise(input))
            return (false, "Repetitive input detected");
        return (true, null);
    }

    private static bool ContainsNullBytes(string input)
    {
        foreach (char c in input)
            if (c == '\0') return true;
        return false;
    }

    private static bool IsRepetitiveNoise(string input)
    {
        if (input.Length < 10) return false;
        int sameCount = 1;
        char lastChar = input[0];
        for (int i = 1; i < input.Length; i++)
        {
            if (input[i] == lastChar)
            {
                sameCount++;
                if (sameCount > MaxRepetitiveChars) return true;
            }
            else
            {
                sameCount = 1;
                lastChar = input[i];
            }
        }
        return false;
    }
}

static class SCPDetector
{
    public static bool IsSCPCommand(string input)
    {
        return input.Contains("scp ", StringComparison.OrdinalIgnoreCase) ||
               input.StartsWith("scp", StringComparison.OrdinalIgnoreCase) ||
               input.Contains("sftp", StringComparison.OrdinalIgnoreCase);
    }
}

static class Logger
{
    private static readonly object _lock = new();
    private static readonly object _metricLock = new();
    private static readonly object _statsLock = new();

    static Logger()
    {
        try { Directory.CreateDirectory(Program.LogDir); } catch { }
    }

    static readonly Lazy<ISerializer> YamlSerializer = new(() =>
        new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build());

    public static void LogYaml(string eventType, object data)
    {
        lock (_lock)
        {
            try
            {
                string yaml = YamlSerializer.Value.Serialize(new Dictionary<string, object>
                {
                    ["timestamp"] = DateTime.UtcNow.ToString("o"),
                    ["event"] = eventType,
                    ["data"] = data
                });
                string path = Path.Combine(Program.LogDir, $"{eventType}.yaml");
                File.AppendAllText(path, "---\n" + yaml);
            }
            catch { }
        }
    }

    public static void UpdateGlobalStats(string username, int messages, int blocked, int promptTokens, int completionTokens, long durationMs)
    {
        lock (_statsLock)
        {
            try
            {
                string statsPath = Path.Combine(Program.AppDir, "frontend", "global_stats.json");
                GlobalStats stats = new();

                if (File.Exists(statsPath))
                {
                    string json = File.ReadAllText(statsPath);
                    stats = JsonSerializer.Deserialize<GlobalStats>(json) ?? new();
                }

                stats.TotalSessions++;
                stats.TotalMessages += messages;
                stats.TotalBlockedOperations += blocked;
                stats.TotalPromptTokens += promptTokens;
                stats.TotalCompletionTokens += completionTokens;
                stats.TotalDurationMs += durationMs;
                stats.LastUpdated = DateTime.UtcNow;

                if (stats.TopUsers.ContainsKey(username))
                    stats.TopUsers[username]++;
                else
                    stats.TopUsers[username] = 1;

                if (stats.TopUsers.Count > 10)
                    stats.TopUsers = stats.TopUsers.OrderByDescending(x => x.Value).Take(10).ToDictionary(x => x.Key, x => x.Value);

                string newJson = JsonSerializer.Serialize(stats, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(statsPath, newJson);
            }
            catch (Exception ex)
            {
                LogMsg($"Failed to update global stats: {ex.Message}");
            }
        }
    }

    static string GetLogFilePath(string? sessionId, string prefix = "app")
    {
        string baseDir = Path.Combine(Program.AppDir, "frontend", "sessions");
        if (!Directory.Exists(baseDir))
            Directory.CreateDirectory(baseDir);

        string sessionName = Environment.GetEnvironmentVariable("SESSION_NAME") ?? "default";
        string dateString = DateTime.Now.ToString("yyyyMMdd");
        string uniquePart = (sessionName == "default" && sessionId != null) ? sessionId[..8] : sessionName;
        return Path.Combine(baseDir, $"{prefix}-{uniquePart}-{dateString}.log");
    }

    public static void LogMsg(string message, string? sessionId = null)
    {
        lock (_lock)
        {
            try
            {
                using var writer = new StreamWriter(GetLogFilePath(sessionId, "app"), append: true);
                writer.WriteLine($"{DateTime.Now:u} [{sessionId ?? "GLOBAL"}] - {message}");
            }
            catch { }
        }
    }

    public static void LogMetric(string sessionId, string eventType, object payload)
    {
        lock (_metricLock)
        {
            try
            {
                string jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = false });
                string logEntry = $"{DateTime.UtcNow:o}|{sessionId}|{eventType}|{jsonPayload}";
                using var writer = new StreamWriter(GetLogFilePath(sessionId, "metrics"), append: true);
                writer.WriteLine(logEntry);
            }
            catch { }
        }
    }

    public static void PushToGit(string sessionId)
    {
        lock (_lock)
        {
            try
            {
                string repoPath = Path.Combine(Program.AppDir, "frontend");
                string? gitToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
                string? gitUser = Environment.GetEnvironmentVariable("GITHUB_USER");

                if (string.IsNullOrEmpty(gitToken) || string.IsNullOrEmpty(gitUser))
                {
                    LogMsg("Git push skipped: GITHUB_TOKEN or GITHUB_USER not set.", sessionId);
                    return;
                }

                if (!Repository.IsValid(repoPath))
                {
                    LogMsg($"Git push skipped: {repoPath} not a valid repository.", sessionId);
                    return;
                }

                using var repo = new Repository(repoPath);
                string metricsFile = GetLogFilePath(sessionId, "metrics");
                string appLogFile = GetLogFilePath(sessionId, "app");
                string statsFile = Path.Combine(repoPath, "global_stats.json");

                Commands.Stage(repo, Path.GetRelativePath(repoPath, metricsFile));
                Commands.Stage(repo, Path.GetRelativePath(repoPath, appLogFile));

                if (File.Exists(statsFile))
                    Commands.Stage(repo, "global_stats.json");

                var author = new Signature(gitUser!, $"{gitUser}@users.noreply.github.com", DateTimeOffset.Now);
                repo.Commit($"Add session data for {sessionId}", author, author);

                repo.Network.Push(repo.Network.Remotes["origin"],
                    @"refs/heads/master:refs/heads/data",
                    new PushOptions
                    {
                        CredentialsProvider = (_, _, _) =>
                            new UsernamePasswordCredentials { Username = gitToken!, Password = "" }
                    });

                LogMsg($"Pushed session {sessionId} data to data branch.", sessionId);
            }
            catch (Exception ex)
            {
                LogMsg($"Git push failed for session {sessionId}: {ex.Message}", sessionId);
            }
        }
    }
}