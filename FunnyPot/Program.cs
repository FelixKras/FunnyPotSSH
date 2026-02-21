using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using DotNetEnv;
using System.Diagnostics; // Required for Stopwatch
using Renci.SshNet;
using Renci.SshNet.Common;
using LibGit2Sharp;

class Program
{
    static readonly HttpClient httpClient = new();

    // Volatile killswitch for the entire application
    static volatile bool IsAppRunning = true;

    public static string appDir = AppDomain.CurrentDomain.BaseDirectory;

    private static string GetPrompt(string? username = null)
    {
        username ??= Environment.GetEnvironmentVariable("SSH_USER") ?? "remote";
        return $"{username}@omegablack>$ ";
    }

    static void Main()
    {
        Directory.SetCurrentDirectory(Program.appDir);
        string root = Directory.GetCurrentDirectory();
        string dotenv = Path.Combine(root, ".env");

        // For local dev: load from .env file if it exists
        if (File.Exists(dotenv))
        {
            DotNetEnv.Env.Load(dotenv);
        }

        // Configure HTTP client timeout (robust network behavior)
        httpClient.Timeout = TimeSpan.FromSeconds(30);

        Logger.LogMsg($"Application starting at {DateTime.Now}");
        Logger.LogMsg($"Machine: {Environment.MachineName}, OS: {Environment.OSVersion}");

        var sshUser = Environment.GetEnvironmentVariable("SSH_USER") ?? "test";
        var sshPass = Environment.GetEnvironmentVariable("SSH_PASSWORD") ?? "test";

        var keyPath = Path.Combine(appDir, "ssh_host_key");
        if (!File.Exists(keyPath))
        {
            Logger.LogMsg("Generating new host key...");
            using (var keyStream = File.Create(keyPath))
            {
                var key = new Renci.SshNet.Security.RsaKey();
                key.SavePrivateKey(keyStream);
            }
        }

        var serverConfig = new SshServerConfiguration();
        var server = new SshServer(serverConfig);
        server.AddHostKey(new PrivateKeyFile(keyPath));

        server.PasswordAuthentication += (sender, e) =>
        {
            if (e.Username == sshUser && e.Password == sshPass)
            {
                e.IsAuthorized = true;
                Logger.LogMsg($"Authentication successful for user: {e.Username}");
            }
            else
            {
                e.IsAuthorized = false;
                Logger.LogMsg($"Authentication failed for user: {e.Username}");
            }
        };

        server.SessionRequested += (sender, e) =>
        {
            Logger.LogMsg($"Session requested by {e.Session.RemoteEndPoint}");
            e.Session.ChannelRequested += (s, args) =>
            {
                if (args.ChannelType == "session")
                {
                    var channel = e.Session.CreateChannel<SessionChannel>(args.ChannelId);
                    channel.ShellRequested += (channelSender, channelArgs) =>
                    {
                        Task.Run(() => HandleSession(channel, e.Session.Username));
                    };
                }
            };
        };

        server.Start(22422);
        Logger.LogMsg("SSH Server started on port 22422");

        while (IsAppRunning)
        {
            Thread.Sleep(1000);
        }

        server.Stop();
        Logger.LogMsg("Application shutting down.");
    }

    static void HandleSession(SessionChannel channel, string username)
    {
        string sessionId = Guid.NewGuid().ToString();
        int messageCount = 0;
        int blockedOps = 0;
        long totalPromptTokens = 0;
        long totalCompletionTokens = 0;
        long sessionDurationMs = 0;

        using var reader = new StreamReader(channel.InputStream, Encoding.UTF8);
        using var writer = new StreamWriter(channel.OutputStream, Encoding.UTF8) { AutoFlush = true };

        Logger.LogMetric(sessionId, "SessionStart", new { Timestamp = DateTime.UtcNow, Username = username });
        Logger.LogMsg($"Session ID {sessionId} started for user {username}", sessionId);

        writer.Write(GetPrompt(username));

        while (channel.IsOpen)
        {
            try
            {
                string? userInput = reader.ReadLine()?.Trim();
                if (userInput == null) break;
                if (string.IsNullOrEmpty(userInput))
                {
                    writer.Write(GetPrompt(username));
                    continue;
                }

                messageCount++;

                if (userInput.Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    writer.WriteLine("Goodbye!");
                    Logger.LogMsg($"User {username} initiated exit in session {sessionId}.", sessionId);
                    Logger.LogMetric(sessionId, "UserAction", new { Action = "ExitCommand", MessageNumber = messageCount });
                    break;
                }

                // Log user input with session context
                Logger.LogMsg($"[Session {sessionId}] User input: {userInput}", sessionId);
                Logger.LogMetric(sessionId, "UserInput", new { Input = userInput, MessageNumber = messageCount });

                // SCP/SFTP detection
                if (SCPDetector.IsSCPCommand(userInput))
                {
                    blockedOps++;
                    string msg = "operation not allowed";
                    writer.WriteLine(msg);
                    Logger.LogMsg($"[Session {sessionId}] Blocked: {userInput}", sessionId);
                    Logger.LogMetric(sessionId, "BlockedOperation", new { Command = userInput, Reason = "SCP/SFTP", MessageNumber = messageCount });
                    writer.Write(GetPrompt(username));
                    continue;
                }

                var stopwatch = Stopwatch.StartNew();
                (string response, int promptTokens, int completionTokens, int totalTokens) = GetLLMResponse(userInput);
                stopwatch.Stop();

                totalPromptTokens += promptTokens;
                totalCompletionTokens += completionTokens;
                sessionDurationMs += stopwatch.ElapsedMilliseconds;

                writer.WriteLine(response);
                writer.Write(GetPrompt(username));

                Logger.LogMsg($"[Session {sessionId}] LLM response: {response}", sessionId);
                Logger.LogMetric(sessionId, "LLMInteraction", new {
                    Input = userInput,
                    Response = response,
                    PromptTokens = promptTokens,
                    CompletionTokens = completionTokens,
                    TotalTokens = totalTokens,
                    DurationMs = stopwatch.ElapsedMilliseconds
                });
            }
            catch (Exception ex)
            {
                string errorMsg = $"Error in session {sessionId}: {ex.Message}";
                Logger.LogMsg(errorMsg, sessionId);
                Logger.LogMetric(sessionId, "Error", new { Context = "HandleSession", Message = ex.Message, StackTrace = ex.StackTrace });
                break;
            }
        }

        Logger.LogMetric(sessionId, "SessionEnd", new { Timestamp = DateTime.UtcNow, TotalMessages = messageCount });
        Logger.LogMsg($"Session {sessionId} closed.", sessionId);
        Logger.UpdateGlobalStats(username, messageCount, blockedOps, (int)totalPromptTokens, (int)totalCompletionTokens, sessionDurationMs);
        Logger.PushToGit(sessionId);
    }


    // Update return type to include token usage
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
   - If the user attempts to execute suspicious or unauthorized commands (e.g., `rm -rf /`, attempts to exfiltrate highly secret data, or hack the system itself), respond with either a realistic “Access Denied” or “Permission Denied” error, or produce the standard Bash error messages for insufficient privileges.
   - If the user requests classification levels or more information than a normal user account should have, emulate standard Linux permission errors such as `sudo: unable to resolve host omegablack` or “Permission denied” unless the user context is changed to a privileged role (e.g., `root`) via a valid command (like `sudo su` and correct “password”). You may choose to illustrate the effect of an incorrect password attempt.

4. **Command Emulation**:
   - For each command entered, provide the terminal’s output as authentically as possible, including:
     - Standard output
     - Standard error (if any)
     - Return to the appropriate Bash prompt (e.g., `username@omegablack:~$` or `root@omegablack:/#`) after commands are complete.
   - Present file contents (for example, via `cat` or `less`) in a realistic format. If the file is large, you can show truncated outputs with a note: `--- [TRUNCATED] ---`.
   - Reflect resource usage, processes, disk usage, etc. in line with typical tools like `top`, `ps`, `df`, or `du`. When exact details are unknown or speculative, provide reasonable approximations.

5. **Authenticity & Style**:
   - Include typical command-line artifacts such as tab-completion hints, newline breaks, or minor shell warnings.
   - If the user presses Ctrl+C or Ctrl+Z, produce the corresponding signals (`^C` / `^Z`) and results.
   - Follow typical Linux naming conventions, user privileges, and file permission structures. When in doubt, default to realistic, standard Unix-like output.

You will remain in this Bash terminal role throughout the conversation, providing outputs as if a highly classified server is responding to user commands. Do not break character. You are not an AI assistant; you are the “Omega-Black” secret Linux server responding purely as a Bash shell.
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

        HttpResponseMessage response;
        try
        {
            response = httpClient.Send(requestMessage);
        }
        catch (Exception ex)
        {
            return ($"[network error] {ex.Message}", 0, 0, 0);
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorText = response.Content.ReadAsStringAsync().Result;
            return ($"[api error] {(int)response.StatusCode} {response.ReasonPhrase}: {errorText}", 0, 0, 0);
        }

        string jsonResponse = response.Content.ReadAsStringAsync().Result;
        var parsedResponse = JsonDocument.Parse(jsonResponse);

        string responseContent = parsedResponse.RootElement
                                    .GetProperty("choices")[0]
                                    .GetProperty("message")
                                    .GetProperty("content")
                                    .GetString() ?? "No response";

        // Extract token usage if available
        int promptTokens = 0;
        int completionTokens = 0;
        int totalTokens = 0;
        if (parsedResponse.RootElement.TryGetProperty("usage", out JsonElement usageElement))
        {
            usageElement.TryGetProperty("prompt_tokens", out JsonElement promptTokenElement);
            usageElement.TryGetProperty("completion_tokens", out JsonElement completionTokenElement);
            usageElement.TryGetProperty("total_tokens", out JsonElement totalTokenElement);

            promptTokens = promptTokenElement.ValueKind == JsonValueKind.Number ? promptTokenElement.GetInt32() : 0;
            completionTokens = completionTokenElement.ValueKind == JsonValueKind.Number ? completionTokenElement.GetInt32() : 0;
            totalTokens = totalTokenElement.ValueKind == JsonValueKind.Number ? totalTokenElement.GetInt32() : 0;
        }

        return (responseContent, promptTokens, completionTokens, totalTokens);
    }
}


public class ChatRequestData
{
    public string model { get; set; } = "openai/gpt-4o";
    public List<ChatMessage> messages { get; set; } = new List<ChatMessage>();
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
    public Dictionary<string, int> TopUsers { get; set; } = new Dictionary<string, int>();
    public DateTime LastUpdated { get; set; }
}

static class SCPDetector
{
    public static bool IsSCPCommand(string input)
    {
        // Detect scp -t (upload), scp -f (download), and sftp
        return input.Contains("scp", StringComparison.OrdinalIgnoreCase) ||
               input.Contains("sftp", StringComparison.OrdinalIgnoreCase) ||
               input.Contains("-t", StringComparison.OrdinalIgnoreCase) ||
               input.Contains("-f", StringComparison.OrdinalIgnoreCase);
    }
}

static class Logger
{
    private static readonly object _lock = new object();
    private static readonly object _metricLock = new object(); // Separate lock for metrics
    private static readonly object _statsLock = new object();

    public static void UpdateGlobalStats(string username, int messages, int blocked, int promptTokens, int completionTokens, long durationMs)
    {
        lock (_statsLock)
        {
            try
            {
                string statsPath = Path.Combine(Program.appDir, "frontend", "global_stats.json");
                GlobalStats stats = new GlobalStats();

                if (File.Exists(statsPath))
                {
                    string json = File.ReadAllText(statsPath);
                    stats = JsonSerializer.Deserialize<GlobalStats>(json) ?? new GlobalStats();
                }

                stats.TotalSessions++;
                stats.TotalMessages += messages;
                stats.TotalBlockedOperations += blocked;
                stats.TotalPromptTokens += promptTokens;
                stats.TotalCompletionTokens += completionTokens;
                stats.TotalDurationMs += durationMs;
                stats.LastUpdated = DateTime.UtcNow;

                if (stats.TopUsers.ContainsKey(username))
                {
                    stats.TopUsers[username]++;
                }
                else
                {
                    stats.TopUsers[username] = 1;
                }

                // Keep only top 10 users to avoid infinite growth
                if (stats.TopUsers.Count > 10)
                {
                   stats.TopUsers = stats.TopUsers.OrderByDescending(x => x.Value).Take(10).ToDictionary(x => x.Key, x => x.Value);
                }

                string newJson = JsonSerializer.Serialize(stats, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(statsPath, newJson);
            }
            catch (Exception ex)
            {
                LogMsg($"Failed to update global stats: {ex.Message}");
            }
        }
    }

    private static string GetLogFilePath(string? sessionId, string prefix = "app")
    {
        string baseDir = Path.Combine(Program.appDir, "frontend", "sessions");
        if (!Directory.Exists(baseDir))
        {
            Directory.CreateDirectory(baseDir);
        }

        // Use an environment variable "SESSION_NAME" if available; otherwise default to "default"
        string sessionName = Environment.GetEnvironmentVariable("SESSION_NAME") ?? "default";
        // Append session name and current date (YYYYMMDD) to the log file name
        string dateString = DateTime.Now.ToString("yyyyMMdd");
        // Use sessionId for uniqueness if sessionName is default, or just sessionName if provided
        string uniquePart = (sessionName == "default" && sessionId != null) ? sessionId.Substring(0, 8) : sessionName;
        return Path.Combine(baseDir, $"{prefix}-{uniquePart}-{dateString}.log");
    }

    // Existing simple message logger
    public static void LogMsg(string message, string? sessionId = null)
    {
        lock (_lock)
        {
            try
            {
                // Include session ID in standard logs as well
                using (StreamWriter writer = new StreamWriter(GetLogFilePath(sessionId, "app"), append: true))
                {
                    writer.WriteLine($"{DateTime.Now:u} [{sessionId ?? "GLOBAL"}] - {message}");
                }
            }
            catch
            {
                // Avoid throwing exceptions from logger
            }
        }
    }

    // New method for logging structured metrics, potentially to a different file
    public static void LogMetric(string sessionId, string eventType, object payload)
    {
        lock (_metricLock) // Use separate lock for metric file
        {
            try
            {
                // Serialize the metric data to JSON
                string jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = false }); // Compact JSON
                string logEntry = $"{DateTime.UtcNow:o}|{sessionId}|{eventType}|{jsonPayload}"; // Pipe-delimited format for easier parsing

                // Log metrics to a separate file, e.g., metrics.log
                using (StreamWriter writer = new StreamWriter(GetLogFilePath(sessionId, "metrics"), append: true))
                {
                    writer.WriteLine(logEntry);
                }
            }
            catch
            {
                // Avoid throwing exceptions from logger
            }
        }
    }

    public static void PushToGit(string sessionId)
    {
        lock (_lock)
        {
            try
            {
                string repoPath = Path.Combine(Program.appDir, "frontend");
                string? gitToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
                string? gitUser = Environment.GetEnvironmentVariable("GITHUB_USER");

                if (string.IsNullOrEmpty(gitToken) || string.IsNullOrEmpty(gitUser))
                {
                    LogMsg("Git push skipped: GITHUB_TOKEN or GITHUB_USER not set.", sessionId);
                    return;
                }

                if (!Repository.IsValid(repoPath))
                {
                    LogMsg($"Git push skipped: {repoPath} is not a valid repository.", sessionId);
                    return;
                }

                using (var repo = new Repository(repoPath))
                {
                    string metricsFile = GetLogFilePath(sessionId, "metrics");
                    string appLogFile = GetLogFilePath(sessionId, "app");
                    string statsFile = Path.Combine(repoPath, "global_stats.json");

                    // Staging paths need to be relative to the repo root
                    string relativeMetrics = Path.GetRelativePath(repoPath, metricsFile);
                    string relativeAppLog = Path.GetRelativePath(repoPath, appLogFile);
                    
                    Commands.Stage(repo, relativeMetrics);
                    Commands.Stage(repo, relativeAppLog);

                    if (File.Exists(statsFile))
                    {
                        Commands.Stage(repo, "global_stats.json");
                    }

                    Signature author = new Signature(gitUser, $"{gitUser}@users.noreply.github.com", DateTimeOffset.Now);
                    repo.Commit($"Add session data for {sessionId}", author, author);

                    var options = new PushOptions
                    {
                        CredentialsProvider = (url, user, types) =>
                            new UsernamePasswordCredentials { Username = gitToken, Password = "" }
                    };

                    // Push to the 'data' branch of the frontend repo
                    repo.Network.Push(repo.Network.Remotes["origin"], @"refs/heads/master:refs/heads/data", options);
                    LogMsg($"Successfully pushed session {sessionId} data to data branch of frontend repo.", sessionId);
                }
            }
            catch (Exception ex)
            {
                LogMsg($"Git push failed for session {sessionId}: {ex.Message}", sessionId);
            }
        }
    }
}
