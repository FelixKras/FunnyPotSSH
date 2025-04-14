using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using DotNetEnv;
using System.Diagnostics; // Required for Stopwatch

class Program
{
    static readonly HttpClient httpClient = new();

    // Shared queue for user input
    static ConcurrentQueue<string> inputQueue = new ConcurrentQueue<string>();
    // Volatile killswitch for thread synchronization
    static volatile bool IsRunning = true;
    // Event used to signal that a new message is available
    static AutoResetEvent inputSignal = new AutoResetEvent(false);

    public static string appDir = AppDomain.CurrentDomain.BaseDirectory;
    // Unique identifier for the current session
    public static string sessionId = Guid.NewGuid().ToString();

    static void Main()
    {
        Directory.SetCurrentDirectory(Program.appDir);
        string root = Directory.GetCurrentDirectory();
        string dotenv = Path.Combine(root, ".env");
        var envForDebug=Env.Load(dotenv);


        // For local dev: load from .env file if it exists
        if (File.Exists(dotenv))
        {
            DotNetEnv.Env.Load(dotenv);
        }


        // Log app start info with session ID
        Logger.LogMetric(sessionId, "SessionStart", new { Timestamp = DateTime.UtcNow });
        Logger.LogMsg($"Application starting at {DateTime.Now}");
        Logger.LogMsg($"Session ID: {sessionId}");
        Logger.LogMsg($"Machine: {Environment.MachineName}, OS: {Environment.OSVersion}");


        Console.WriteLine("remote@omegablack>$");
        Console.Out.Flush();

        // Start a long-running background thread to process input
        Thread processorThread = new Thread(ProcessInputQueue);
        processorThread.Start();

        int messageCount = 0; // Track messages within the session

        while (IsRunning)
        {
            try
            {
                string userInput = (Console.ReadLine() ?? "").Trim();
                if (string.IsNullOrEmpty(userInput))
                    continue;

                messageCount++;

                if (userInput.Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Goodbye!");
                    Logger.LogMsg("User initiated exit.");
                    Logger.LogMetric(sessionId, "UserAction", new { Action = "ExitCommand", MessageNumber = messageCount });
                    IsRunning = false;
                    // Signal the background thread so it can exit if waiting
                    inputSignal.Set();
                    break;
                }

                // Log user input with session context
                Logger.LogMsg($"User input: {userInput}");
                Logger.LogMetric(sessionId, "UserInput", new { Input = userInput, MessageNumber = messageCount });


                // SCP/SFTP detection remains in the main thread
                if (SCPDetector.IsSCPCommand(userInput))
                {
                    string msg = "operation not allowed";
                    Console.WriteLine(msg);
                    Logger.LogMsg(msg);
                    Logger.LogMetric(sessionId, "BlockedOperation", new { Command = userInput, Reason = "SCP/SFTP", MessageNumber = messageCount });
                    continue;
                }

                // Enqueue the input and signal the background thread
                inputQueue.Enqueue(userInput);
                inputSignal.Set();
            }
            catch (Exception ex)
            {
                string errorMsg = $"Error: {ex.Message}";
                Console.WriteLine(errorMsg);
                Logger.LogMsg(errorMsg);
                Logger.LogMetric(sessionId, "Error", new { Context = "MainLoop", Message = ex.Message, StackTrace = ex.StackTrace });
                IsRunning = false;
                inputSignal.Set();
                break;
            }
        }

        // Wait for the background thread to finish processing before exiting
        processorThread.Join();
        Logger.LogMetric(sessionId, "SessionEnd", new { Timestamp = DateTime.UtcNow, TotalMessages = messageCount });
        Logger.LogMsg("Application shutting down.");
    }
    static void ProcessInputQueue()
    {
        while (IsRunning || !inputQueue.IsEmpty)
        {
            // Wait until a new input is signaled or timeout to check IsRunning
            inputSignal.WaitOne(TimeSpan.FromSeconds(1)); // Add timeout

            while (inputQueue.TryDequeue(out string? userInput))
            {
                if (userInput is null)
                    continue;
                try
                {
                    var stopwatch = Stopwatch.StartNew();
                    (string response, int promptTokens, int completionTokens, int totalTokens) = GetLLMResponse(userInput);
                    stopwatch.Stop();

                    Console.WriteLine(response+"\nremote@omegablack>$");
                    Logger.LogMsg($"LLM response: {response}");
                    Logger.LogMetric(sessionId, "LLMInteraction", new {
                        Input = userInput,
                        Response = response, // Consider truncating long responses for logs
                        PromptTokens = promptTokens,
                        CompletionTokens = completionTokens,
                        TotalTokens = totalTokens,
                        DurationMs = stopwatch.ElapsedMilliseconds
                    });
                    Console.Out.Flush();
                }
                catch (Exception ex)
                {
                    string errorMsg = $"Error processing input '{userInput}': {ex.Message}";
                    Console.WriteLine(errorMsg);
                    Logger.LogMsg(errorMsg);
                    Logger.LogMetric(sessionId, "Error", new { Context = "ProcessInputQueue", Input = userInput, Message = ex.Message, StackTrace = ex.StackTrace });
                }
            }
        }
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

        var response = httpClient.Send(requestMessage);
        response.EnsureSuccessStatusCode();

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


    private static string GetLogFilePath(string prefix = "app")
    {
        Directory.SetCurrentDirectory(Program.appDir);

        // Use an environment variable "SESSION_NAME" if available; otherwise default to "default"
        string sessionName = Environment.GetEnvironmentVariable("SESSION_NAME") ?? "default";
        // Append session name and current date (YYYYMMDD) to the log file name
        string dateString = DateTime.Now.ToString("yyyyMMdd");
        // Use Program.sessionId for uniqueness if sessionName is default, or just sessionName if provided
        string uniquePart = (sessionName == "default") ? Program.sessionId.Substring(0, 8) : sessionName;
        return Path.Combine(Directory.GetCurrentDirectory(), $"{prefix}-{uniquePart}-{dateString}.log");
    }

    // Existing simple message logger
    public static void LogMsg(string message)
    {
        lock (_lock)
        {
            try
            {
                // Include session ID in standard logs as well
                using (StreamWriter writer = new StreamWriter(GetLogFilePath("app"), append: true))
                {
                    writer.WriteLine($"{DateTime.Now:u} [{Program.sessionId}] - {message}");
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
                using (StreamWriter writer = new StreamWriter(GetLogFilePath("metrics"), append: true))
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
}
