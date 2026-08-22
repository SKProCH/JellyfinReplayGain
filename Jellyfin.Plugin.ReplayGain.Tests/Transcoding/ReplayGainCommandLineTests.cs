using AwesomeAssertions;
using Jellyfin.Plugin.ReplayGain;

namespace Jellyfin.Plugin.ReplayGain.Tests.Transcoding;

public sealed class ReplayGainCommandLineTests
{
    [Fact]
    public void TryAppend_WithoutAudioFilter_AddsReplayGainFilter()
    {
        var command = "-i \"music file.flac\" -codec:a aac -y \"output file.m4a\"";

        ReplayGainTranscodeManager.ReplayGainCommandLine.TryAppend(command, out var updated).Should().BeTrue();

        updated.Should().Be("-i \"music file.flac\" -codec:a aac -af \"volume=replaygain=track\" -y \"output file.m4a\"");
    }

    [Fact]
    public void TryAppend_WithExistingAudioFilter_ComposesOneFilterChain()
    {
        var command = "-i input.flac -af \"asetpts=PTS-0/TB\" -codec:a aac -y output.m4a";

        ReplayGainTranscodeManager.ReplayGainCommandLine.TryAppend(command, out var updated).Should().BeTrue();

        updated.Should().Be("-i input.flac -af \"asetpts=PTS-0/TB,volume=replaygain=track\" -codec:a aac -y output.m4a");
        updated.Should().Contain("-af ").And.Contain("asetpts=PTS-0/TB");
        updated.Should().NotContain("-af \"asetpts=PTS-0/TB\" -af");
    }

    [Fact]
    public void TryAppend_WhenAlreadyPresent_IsIdempotent()
    {
        var command = "-i input.flac -af \"volume=replaygain=track\" -codec:a aac";

        ReplayGainTranscodeManager.ReplayGainCommandLine.TryAppend(command, out var updated).Should().BeTrue();

        updated.Should().Be(command);
    }

    [Fact]
    public void TryAppend_PreservesQuotedArguments()
    {
        var command = "-i \"C:\\Music\\Track One.flac\" -metadata \"title=Track One\" -y output.m4a";

        ReplayGainTranscodeManager.ReplayGainCommandLine.TryAppend(command, out var updated).Should().BeTrue();

        updated.Should().StartWith("-i \"C:\\Music\\Track One.flac\" -metadata \"title=Track One\"");
        updated.Should().Contain("\"C:\\Music\\Track One.flac\"");
        updated.Should().Contain("\"title=Track One\"");
    }

    [Fact]
    public void TryAppend_WhenQuotesAreUnbalanced_LeavesCommandUnchanged()
    {
        var command = "-i \"broken input.flac -codec:a aac -y output.m4a";

        ReplayGainTranscodeManager.ReplayGainCommandLine.TryAppend(command, out var updated).Should().BeFalse();

        updated.Should().Be(command);
    }

    [Fact]
    public void TryAppend_WhenAudioFilterArgumentIsMissing_LeavesCommandUnchanged()
    {
        var command = "-i input.flac -af";

        ReplayGainTranscodeManager.ReplayGainCommandLine.TryAppend(command, out var updated).Should().BeFalse();

        updated.Should().Be(command);
    }
}
