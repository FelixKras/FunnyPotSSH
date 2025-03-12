using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.Generic;
using DotNetEnv;

class Program
{
    private static readonly HttpClient httpClient = new HttpClient();
    private static bool killswitch = true;

    // Shared queue for user input
    private static readonly ConcurrentQueue<string> inputQueue = new ConcurrentQueue<string>();
    // Event used to signal that a new message is available
    private static readonly AutoResetEvent inputSignal = new AutoResetEvent(false);

    // Conversation history record
    private static readonly List<ChatMessage> conversationHistory = new List<ChatMessage>();

    static void Main()
    {
        var root = Directory.GetCurrentDirectory();

        #if RELEASE
        var dotenv = Path.Combine(root, ".env");
        #else
        var dotenv = Path.Combine(root, "../../../.env");
        #endif
      
        DotNetEnv.Env.Load(dotenv);

        // Initialize conversation history with the system prompt
        conversationHistory.Add(new ChatMessage
        {
            Role = "system",
            Content = "You are a remote computer terminal of top secret linux server. Respond with terminal command output."
        });

        Console.WriteLine("remote@gwSrvr ~ $");
        Console.Out.Flush();

        // Start a long-running background thread to process input
        Thread processorThread = new Thread(ProcessInputQueue);
        processorThread.Start();

        while (killswitch)
        {
            try
            {
                string userInput = (Console.ReadLine() ?? "").Trim();
                if (string.IsNullOrEmpty(userInput))
                    continue;

                if (userInput.Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Goodbye!");
                    killswitch = false;
                    // Signal the background thread so it can exit if waiting
                    inputSignal.Set();
                    break;
                }

                // SCP/SFTP detection remains in the main thread
                if (SCPDetector.IsSCPCommand(userInput))
                {
                    Console.WriteLine("❌ File transfers are not allowed.");
                    continue;
                }

                // Enqueue the input and signal the background thread
                inputQueue.Enqueue(userInput);
                inputSignal.Set();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                killswitch = false;
                inputSignal.Set();
                break;
            }
        }

        // Wait for the background thread to finish processing before exiting
        processorThread.Join();
    }

    static void ProcessInputQueue()
    {
        while (killswitch || !inputQueue.IsEmpty)
        {
            // Wait until a new input is signaled
            inputSignal.WaitOne();

            while (inputQueue.TryDequeue(out string? userInput))
            {
                if (userInput is null)
                    continue;
                try
                {
                    // Add user input to conversation history
                    conversationHistory.Add(new ChatMessage
                    {
                        Role = "user",
                        Content = userInput
                    });

                    string response = GetLLMResponse();
                    
                    // Add LLM response to conversation history
                    conversationHistory.Add(new ChatMessage
                    {
                        Role = "assistant",
                        Content = response
                    });
                    
                    Console.WriteLine(response);
                    Console.Out.Flush();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error: {ex.Message}");
                }
            }
        }
    }

    static string GetLLMResponse()
    {
        string apiUrl = "https://openrouter.ai/api/v1/chat/completions";
        string apiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_key") ?? "no-key";
        
        var requestData = new
        {
            model = "openai/gpt-4o",
            messages = conversationHistory,
            max_tokens = 2000
        };

        string jsonRequest = JsonSerializer.Serialize(requestData);
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, apiUrl);
        requestMessage.Headers.Add("Authorization", $"Bearer {apiKey}");
        requestMessage.Content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

        var response = httpClient.Send(requestMessage);
        response.EnsureSuccessStatusCode();

        string jsonResponse = response.Content.ReadAsStringAsync().Result;
        var parsedResponse = JsonDocument.Parse(jsonResponse);

        return parsedResponse.RootElement.GetProperty("choices")[0]
                             .GetProperty("message")
                             .GetProperty("content").GetString() ?? "No response";
    }
}

class SCPDetector
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

public class ChatMessage
{
    public string Role { get; set; }
    public string Content { get; set; }
}
