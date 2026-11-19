using Xunit;

namespace FunnyPot.Tests;

public class ResponseAssetRebuilderTests
{
    [Fact]
    public void CanonicalCommandsUseSyntheticValues()
    {
        var command = ResponseAssetRebuilder.Materialize("curl http://{{HOST_1}}:{{PORT_1}}/x -o /tmp/{{TMP_FILE_1}}");
        Assert.Equal("curl http://198.51.100.21:8081/x -o /tmp/terra_probe_1", command);
    }

    [Fact]
    public void UnknownPlaceholdersFailRatherThanLeakUnexpandedTemplates()
    {
        Assert.Throws<InvalidDataException>(() => ResponseAssetRebuilder.Materialize("{{UNKNOWN_1}}"));
    }

    [Theory]
    [InlineData("nproc", "2")]
    [InlineData("uname -a", "Linux wrongkernel")]
    [InlineData("cat /proc/cpuinfo", "processor\t: 0")]
    [InlineData("cat /etc/os-release", "NAME=Ubuntu")]
    [InlineData("free", "1024")]
    [InlineData("df -h", "/dev/root /")]
    [InlineData("pwd", "[api error] timeout")]
    [InlineData("cat /proc/cpuinfo", "--- [TRUNCATED] ---")]
    public void IncorrectGeneratedBaselineIsRejected(string command, string response)
    {
        Assert.NotNull(ResponseAssetRebuilder.ValidateResponse(command, response));
    }

    [Fact]
    public void CorrectCpuCountIsAccepted()
    {
        Assert.Null(ResponseAssetRebuilder.ValidateResponse("nproc", "96"));
    }

    [Fact]
    public void BinaryTerminalJunkMayBeIntentionallyTruncated()
    {
        Assert.Null(ResponseAssetRebuilder.ValidateResponse("cat /bin/echo", "\u007fELF\n--- [TRUNCATED] ---"));
    }

    [Fact]
    public void AuthoritativeFileValidationToleratesTerminalLineEndings()
    {
        var content = FakeFileSystem.GetOrCreate("test-file-line-endings").ReadFile("/etc/os-release")!;
        Assert.Null(ResponseAssetRebuilder.ValidateResponse("cat /etc/os-release", content));
        Assert.Null(ResponseAssetRebuilder.ValidateResponse("cat /etc/os-release", content.Replace("\n", "\r\n")));
    }
}
