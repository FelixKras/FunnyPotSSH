using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using OpenAI.Chat;
using DotNetEnv;


class Program
{
    private static readonly HttpClient httpClient = new HttpClient();
    private static bool killswitch = true;
    

    static void Main()
    {
        
        var root = Directory.GetCurrentDirectory();
        var dotenv = Path.Combine(root, "../../../.env");
        DotNetEnv.Env.Load(dotenv);

        Console.WriteLine("🚀 Welcome to Secure AI Chat! Type 'exit' to quit.");
        Console.Out.Flush();

        while (killswitch)
        {
            try
            {
                string userInput = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(userInput))
                    continue;

                if (userInput.Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Goodbye!");
                    break;
                }

                // SCP/SFTP detection
                if (SCPDetector.IsSCPCommand(userInput))
                {
                    Console.WriteLine("❌ File transfers are not allowed.");
                    return;
                }

                // Create a new thread to handle the LLM API call
                Thread thread = new Thread(() => HandleUserInput(userInput));
                thread.Start();
                thread.Join(); // Wait for the thread to complete before continuing
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                return; // Ensures session closes on failure
            }
        }
    }

    static void HandleUserInput(string userInput)
    {
        try
        {
            string response = GetLLMResponse(userInput);
            Console.WriteLine(response);
            Console.Out.Flush();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
        }
    }

    static string GetLLMResponse(string userInput)
    {
        string apiUrl = "https://openrouter.ai/api/v1/chat/completions";
        string apiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_key") ?? "no-key";
        var requestData = new
        {
            model = "openai/gpt-4o",
            messages = new[] { new { role = "user", content = userInput } },
            max_tokens = 200
        };

        string jsonRequest = JsonSerializer.Serialize(requestData);
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, apiUrl);
        requestMessage.Headers.Add("Authorization", $"Bearer {apiKey}");
        requestMessage.Content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

        var response = httpClient.Send(requestMessage);
        response.EnsureSuccessStatusCode();

        string jsonResponse = response.Content.ReadAsStringAsync().Result;
        var parsedResponse = JsonDocument.Parse(jsonResponse);
        return parsedResponse.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "No response";
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
