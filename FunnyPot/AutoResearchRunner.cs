using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FunnyPot;

internal sealed class AutoResearchRunner
{
    private readonly AutoResearchConfig _config;
    private readonly TextWriter _output;

    public AutoResearchRunner(AutoResearchConfig config, TextWriter? output = null)
    {
        _config = config;
        _output = output ?? Console.Out;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var worktree = Path.GetFullPath(_config.WorktreePath);
        ValidateMutablePaths(worktree, _config.MutablePaths);

        var statePath = ResolveInsideWorktree(worktree, _config.StatePath);
        var state = LoadState(statePath);
        var programPath = ResolveInsideWorktree(worktree, _config.ProgramPath);
        if (!File.Exists(programPath))
            throw new FileNotFoundException($"AutoResearch program file not found: {programPath}");

        var setupResult = await RunCommandAsync(_config.SetupCommand, worktree, _config.TimeBudgetSeconds, cancellationToken).ConfigureAwait(false);
        if (setupResult.ExitCode != 0)
        {
            await _output.WriteLineAsync(setupResult.CombinedOutput).ConfigureAwait(false);
            await _output.WriteLineAsync("AutoResearch setup failed.").ConfigureAwait(false);
            return setupResult.ExitCode;
        }

        var exitCode = 0;
        for (var iteration = 1; iteration <= Math.Max(1, _config.Iterations); iteration++)
        {
            await _output.WriteLineAsync($"AutoResearch iteration {iteration}/{Math.Max(1, _config.Iterations)}").ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(_config.AgentCommand))
            {
                var prompt = BuildAgentPrompt(File.ReadAllText(programPath), state.BestMetric);
                var agentResult = await RunCommandAsync(_config.AgentCommand, worktree, _config.TimeBudgetSeconds, cancellationToken, prompt).ConfigureAwait(false);
                if (agentResult.ExitCode != 0)
                {
                    await _output.WriteLineAsync(agentResult.CombinedOutput).ConfigureAwait(false);
                    await RejectCandidateAsync(worktree, $"agent command failed with exit code {agentResult.ExitCode}").ConfigureAwait(false);
                    exitCode = agentResult.ExitCode;
                    continue;
                }
            }

            var experiment = await RunCommandAsync(_config.ExperimentCommand, worktree, _config.TimeBudgetSeconds, cancellationToken).ConfigureAwait(false);
            var metric = TryParseMetric(experiment.CombinedOutput, _config.MetricRegex, out var parsedMetric)
                ? parsedMetric
                : (experiment.ExitCode == 0 ? 1d : 0d);
            var improved = experiment.ExitCode == 0 && IsImprovement(metric, state.BestMetric, _config.LowerIsBetter);

            if (improved)
            {
                state.BestMetric = metric;
                state.LastImprovedAtUtc = DateTime.UtcNow;
                SaveState(statePath, state);
                await AcceptCandidateAsync(worktree, metric).ConfigureAwait(false);
                await _output.WriteLineAsync($"AutoResearch accepted metric={metric.ToString(CultureInfo.InvariantCulture)}").ConfigureAwait(false);
            }
            else
            {
                if (experiment.ExitCode != 0)
                    await _output.WriteLineAsync(experiment.CombinedOutput).ConfigureAwait(false);
                await RejectCandidateAsync(worktree, experiment.ExitCode == 0 ? $"metric {metric} did not improve" : $"experiment failed with exit code {experiment.ExitCode}").ConfigureAwait(false);
                if (experiment.ExitCode != 0)
                    exitCode = experiment.ExitCode;
            }
        }

        return exitCode;
    }

    internal static bool IsImprovement(double candidate, double? best, bool lowerIsBetter)
    {
        if (best is null)
            return true;
        return lowerIsBetter ? candidate < best.Value : candidate > best.Value;
    }

    internal static bool TryParseMetric(string output, string pattern, out double metric)
    {
        metric = 0;
        var match = Regex.Match(output, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
        if (!match.Success)
            return false;

        var value = match.Groups["value"].Success ? match.Groups["value"].Value : match.Value;
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out metric);
    }

    internal static void ValidateMutablePaths(string worktree, IEnumerable<string> mutablePaths)
    {
        foreach (var path in mutablePaths)
            _ = ResolveInsideWorktree(worktree, path);
    }

    private async Task AcceptCandidateAsync(string worktree, double metric)
    {
        if (!_config.AllowGitMutations)
            return;

        foreach (var path in _config.MutablePaths)
            await RunCommandAsync($"git add -- {Quote(path)}", worktree, _config.TimeBudgetSeconds, CancellationToken.None).ConfigureAwait(false);

        var message = $"autoresearch: metric {metric.ToString(CultureInfo.InvariantCulture)}";
        await RunCommandAsync($"git commit -m {Quote(message)}", worktree, _config.TimeBudgetSeconds, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task RejectCandidateAsync(string worktree, string reason)
    {
        await _output.WriteLineAsync($"AutoResearch rejected: {reason}").ConfigureAwait(false);
        if (!_config.AllowGitMutations)
            return;

        foreach (var path in _config.MutablePaths)
            await RunCommandAsync($"git restore -- {Quote(path)}", worktree, _config.TimeBudgetSeconds, CancellationToken.None).ConfigureAwait(false);
    }

    private static string BuildAgentPrompt(string program, double? bestMetric)
    {
        var best = bestMetric is null ? "none yet" : bestMetric.Value.ToString(CultureInfo.InvariantCulture);
        return $"{program}\n\nCurrent best metric: {best}\nModify only the configured mutable files, then stop.";
    }

    private static AutoResearchState LoadState(string statePath)
    {
        try
        {
            if (!File.Exists(statePath))
                return new AutoResearchState();
            return JsonSerializer.Deserialize<AutoResearchState>(File.ReadAllText(statePath)) ?? new AutoResearchState();
        }
        catch
        {
            return new AutoResearchState();
        }
    }

    private static void SaveState(string statePath, AutoResearchState state)
    {
        var directory = Path.GetDirectoryName(statePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(statePath, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static async Task<CommandResult> RunCommandAsync(string command, string workingDirectory, int timeoutSeconds, CancellationToken cancellationToken, string? standardInput = null)
    {
        if (string.IsNullOrWhiteSpace(command))
            return new CommandResult(0, "");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds)));

        var startInfo = new ProcessStartInfo
        {
            FileName = "/bin/sh",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = standardInput is not null,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("-lc");
        startInfo.ArgumentList.Add(command);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Failed to start command: {command}");
        if (standardInput is not null)
        {
            await process.StandardInput.WriteAsync(standardInput.AsMemory(), timeout.Token).ConfigureAwait(false);
            process.StandardInput.Close();
        }

        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            var timedOutOutput = new StringBuilder();
            timedOutOutput.AppendLine($"Command timed out after {Math.Max(1, timeoutSeconds)} seconds: {command}");
            timedOutOutput.Append(await stdout.ConfigureAwait(false));
            timedOutOutput.Append(await stderr.ConfigureAwait(false));
            return new CommandResult(124, timedOutOutput.ToString());
        }

        var combined = new StringBuilder();
        combined.Append(await stdout.ConfigureAwait(false));
        combined.Append(await stderr.ConfigureAwait(false));
        return new CommandResult(process.ExitCode, combined.ToString());
    }

    private static string ResolveInsideWorktree(string worktree, string path)
    {
        var fullPath = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(worktree, path));
        var normalizedWorktree = worktree.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(normalizedWorktree, StringComparison.Ordinal) && !string.Equals(fullPath, worktree.TrimEnd(Path.DirectorySeparatorChar), StringComparison.Ordinal))
            throw new InvalidOperationException($"Path escapes AutoResearch worktree: {path}");
        return fullPath;
    }

    private static string Quote(string value) => "'" + value.Replace("'", "'\\''") + "'";

    private sealed record CommandResult(int ExitCode, string CombinedOutput);

    private sealed class AutoResearchState
    {
        public double? BestMetric { get; set; }
        public DateTime? LastImprovedAtUtc { get; set; }
    }
}
