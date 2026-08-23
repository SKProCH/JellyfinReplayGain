namespace Jellyfin.Plugin.ReplayGain.Tests.Transcoding;

public sealed class ReplayGainCommandLineTests {
    [Fact]
    public void TryReplaceAudioCopyCodec_ReplacesUnindexedAudioCodecOnly() {
        var command = "-i input.mkv -map 0:v -map 0:a -c:v copy -c:a copy -c:s copy output.mkv";

        ReplayGainTranscodeManager.ReplayGainCommandLine.TryReplaceAudioCopyCodec(command, "aac", out var updated)
            .Should().BeTrue();

        updated.Should().Be("-i input.mkv -map 0:v -map 0:a -c:v copy -c:a aac -c:s copy output.mkv");
    }

    [Fact]
    public void TryReplaceAudioCopyCodec_ReplacesIndexedAudioCodec() {
        var command = "-i input.mkv -c:v copy -codec:a:0 copy -c:a:1 copy output.mkv";

        ReplayGainTranscodeManager.ReplayGainCommandLine.TryReplaceAudioCopyCodec(command, "libopus", out var updated)
            .Should().BeTrue();

        updated.Should().Be("-i input.mkv -c:v copy -codec:a:0 libopus -c:a:1 libopus output.mkv");
    }

    [Fact]
    public void TryReplaceAudioCopyCodec_WhenNoAudioCopy_LeavesCommandUnchanged() {
        var command = "-i input.mkv -c:v copy -c:s copy output.mkv";

        ReplayGainTranscodeManager.ReplayGainCommandLine.TryReplaceAudioCopyCodec(command, "aac", out var updated)
            .Should().BeFalse();

        updated.Should().Be(command);
    }

    [Fact]
    public void TryReplaceAudioCopyCodec_WhenQuotesAreUsed_PreservesQuotedValueShape() {
        var command = "-i input.mkv -c:a \"copy\" -c:v copy output.mkv";

        ReplayGainTranscodeManager.ReplayGainCommandLine.TryReplaceAudioCopyCodec(command, "aac", out var updated)
            .Should().BeTrue();

        updated.Should().Be("-i input.mkv -c:a \"aac\" -c:v copy output.mkv");
    }

    [Fact]
    public void TryPrependFilter_WithoutAudioFilter_AddsFilter() {
        var command = "-i \"music file.flac\" -codec:a aac -y \"output file.m4a\"";

        ReplayGainTranscodeManager.ReplayGainCommandLine.TryPrependFilter(command, "loudnorm=I=-16", out var updated)
            .Should().BeTrue();

        updated.Should().Be("-i \"music file.flac\" -codec:a aac -af \"loudnorm=I=-16\" -y \"output file.m4a\"");
    }

    [Fact]
    public void TryPrependFilter_WithExistingAudioFilter_ComposesOneFilterChain() {
        var command = "-i input.flac -af \"asetpts=PTS-0/TB\" -codec:a aac -y output.m4a";

        ReplayGainTranscodeManager.ReplayGainCommandLine.TryPrependFilter(command, "loudnorm=I=-16", out var updated)
            .Should().BeTrue();

        updated.Should().Be("-i input.flac -af \"loudnorm=I=-16,asetpts=PTS-0/TB\" -codec:a aac -y output.m4a");
        updated.Should().Contain("-af ").And.Contain("asetpts=PTS-0/TB");
        updated.Should().NotContain("-af \"asetpts=PTS-0/TB\" -af");
    }

    [Fact]
    public void TryPrependFilter_WithDownmixFilters_NormalizesBeforeDownmixAndBoost() {
        var command = "-i input.mkv -af \"pan=stereo|c0=c0|c1=c1,volume=2\" -codec:a aac -y output.m4a";

        ReplayGainTranscodeManager.ReplayGainCommandLine.TryPrependFilter(command, "loudnorm=I=-16", out var updated)
            .Should().BeTrue();

        updated.Should().Be(
            "-i input.mkv -af \"loudnorm=I=-16,pan=stereo|c0=c0|c1=c1,volume=2\" -codec:a aac -y output.m4a");
    }

    [Fact]
    public void TryPrependFilter_WhenAlreadyPresent_IsIdempotent() {
        var command = "-i input.flac -af \"loudnorm=I=-16\" -codec:a aac";

        ReplayGainTranscodeManager.ReplayGainCommandLine.TryPrependFilter(command, "loudnorm=I=-16", out var updated)
            .Should().BeTrue();

        updated.Should().Be(command);
    }

    [Fact]
    public void TryPrepend_PreservesQuotedArguments() {
        var command = "-i \"C:\\Music\\Track One.flac\" -metadata \"title=Track One\" -y output.m4a";

        ReplayGainTranscodeManager.ReplayGainCommandLine.TryPrependFilter(command, "loudnorm=I=-16", out var updated)
            .Should().BeTrue();

        updated.Should().StartWith("-i \"C:\\Music\\Track One.flac\" -metadata \"title=Track One\"");
        updated.Should().Contain("\"C:\\Music\\Track One.flac\"");
        updated.Should().Contain("\"title=Track One\"");
    }

    [Fact]
    public void TryPrepend_WhenQuotesAreUnbalanced_LeavesCommandUnchanged() {
        var command = "-i \"broken input.flac -codec:a aac -y output.m4a";

        ReplayGainTranscodeManager.ReplayGainCommandLine.TryPrependFilter(command, "loudnorm=I=-16", out var updated)
            .Should().BeFalse();

        updated.Should().Be(command);
    }

    [Fact]
    public void TryPrepend_WhenAudioFilterArgumentIsMissing_LeavesCommandUnchanged() {
        var command = "-i input.flac -af";

        ReplayGainTranscodeManager.ReplayGainCommandLine.TryPrependFilter(command, "loudnorm=I=-16", out var updated)
            .Should().BeFalse();

        updated.Should().Be(command);
    }
}