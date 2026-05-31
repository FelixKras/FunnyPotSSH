using System;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FunnyPot
{
    public class AppConfiguration
    {
        public SshConfig Ssh { get; set; } = new();
        public LlmConfig Llm { get; set; } = new();
        public LoggingConfig Logging { get; set; } = new();
        public DataHarvesterConfig DataHarvester { get; set; } = new();
        public ApiConfig Api { get; set; } = new();
        public GitConfig Git { get; set; } = new();
        public StaticResponsesConfig StaticResponses { get; set; } = new();

        public static AppConfiguration Load(string? configPath = null)
        {
            if (configPath == null)
            {
                // Default to config/app-config.yaml relative to app directory
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                configPath = Path.Combine(appDir, "config", "app-config.yaml");
            }

            if (!File.Exists(configPath))
            {
                // Return default configuration if file doesn't exist
                return new AppConfiguration();
            }

            try
            {
                var yaml = File.ReadAllText(configPath);
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(HyphenatedNamingConvention.Instance)
                    .Build();

                return deserializer.Deserialize<AppConfiguration>(yaml);
            }
            catch (Exception ex)
            {
                // Log the error but don't crash - fall back to defaults
                Console.Error.WriteLine($"Warning: Failed to load configuration from {configPath}: {ex.Message}");
                return new AppConfiguration();
            }
        }
    }

    public class SshConfig
    {
        public int Port { get; set; } = 22422;
        public string Banner { get; set; } = "SSH-2.0-OmegaBlack_Classified_Server_v1.0";
        public string User { get; set; } = "remote";
        public string Password { get; set; } = "test";
        public int BannerRotationInterval { get; set; } = 0;
        public int MaxSessions { get; set; } = 50;
        public int SessionIdleTimeoutSeconds { get; set; } = 300;
        public int AuthMaxTries { get; set; } = 3;
        public int PasswordHarvestAttempt { get; set; } = 3;
    }

    public class LlmConfig
    {
        public int DelayMs { get; set; } = 500;
        public int RateLimitMaxPerWindow { get; set; } = 20;
        public int RateLimitWindowSeconds { get; set; } = 60;
        public string Model { get; set; } = "google/gemma-4-31b-it:free";
        public int MaxTokens { get; set; } = 2000;
        public int TimeoutSeconds { get; set; } = 30;
    }

    public class LoggingConfig
    {
        public string LogDir { get; set; } = "/var/log/funnypot";
    }

    public class DataHarvesterConfig
    {
        public int MaxInputLength { get; set; } = 4096;
        public int MaxRepetitiveChars { get; set; } = 100;
        public long MaxPayloadCaptureBytes { get; set; } = 10485760; // 10 MB
    }

    public class ApiConfig
    {
        public OpenRouterConfig OpenRouter { get; set; } = new();
    }

    public class OpenRouterConfig
    {
        public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1";
        public string AuthEndpoint { get; set; } = "/auth/key";
        public string ChatEndpoint { get; set; } = "/chat/completions";
    }

    public class GitConfig
    {
        public string DataBranch { get; set; } = "data";
        public string User { get; set; } = "";
        public string Token { get; set; } = "";
        public int DataPushIntervalSeconds { get; set; } = 300;
    }

    public class StaticResponsesConfig
    {
        public string DataPath { get; set; } = "data/ssh_responses.jsonl";
    }
}
