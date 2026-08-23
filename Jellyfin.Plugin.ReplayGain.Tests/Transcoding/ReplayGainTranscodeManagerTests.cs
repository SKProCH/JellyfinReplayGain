using Jellyfin.Plugin.ReplayGain.Loudnorm;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.IO;
using Microsoft.Extensions.Configuration;
using MediaBrowser.Controller.Streaming;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Jellyfin.Plugin.ReplayGain.Tests.Transcoding;

public sealed class ReplayGainTranscodeManagerTests {
    [Fact]
    public void CalculatePeakSafeGain_WhenLoudnessGainWouldExceedTruePeak_UsesPeakLimit() {
        ReplayGainTranscodeManager.CalculatePeakSafeGain(-16, -1.5, -18.59, 0.05)
            .Should().BeApproximately(-1.55, 0.001);
    }

    [Fact]
    public void CalculatePeakSafeGain_WhenTruePeakHasHeadroom_UsesLoudnessGain() {
        ReplayGainTranscodeManager.CalculatePeakSafeGain(-16, -1.5, -20, -10)
            .Should().BeApproximately(4, 0.001);
    }

    [Fact]
    public async Task StartFfMpeg_WhenDisabled_PassesOriginalCommand() {
        var inner = CreateInner(out var receivedCommands);
        var manager = CreateManager(inner.Object, false);
        var state = CreateAudioState(inner.Object);
        using var cancellation = new CancellationTokenSource();

        await manager.StartFfMpeg(state, "output.m4a", "-i input.flac -codec:a aac", Guid.Empty,
            TranscodingJobType.Progressive, cancellation);

        receivedCommands.Should().ContainSingle().Which.Should().Be("-i input.flac -codec:a aac");
    }

    [Fact]
    public async Task StartFfMpeg_WhenEnabledWithoutStoredGain_PassesOriginalCommand() {
        var inner = CreateInner(out var receivedCommands);
        var manager = CreateManager(inner.Object, true);
        var state = CreateAudioState(inner.Object);
        using var cancellation = new CancellationTokenSource();

        await manager.StartFfMpeg(state, "output.m4a", "-i input.flac -codec:a aac -y output.m4a", Guid.Empty,
            TranscodingJobType.Progressive, cancellation);

        receivedCommands.Should().ContainSingle().Which.Should().Be("-i input.flac -codec:a aac -y output.m4a");
    }

    [Fact]
    public async Task StartFfMpeg_WhenAudioIsCopied_PassesOriginalCommand() {
        var inner = CreateInner(out var receivedCommands);
        var manager = CreateManager(inner.Object, true);
        var state = CreateAudioState(inner.Object);
        state.OutputAudioCodec = "copy";
        using var cancellation = new CancellationTokenSource();

        await manager.StartFfMpeg(state, "output.m4a", "-i input.flac -codec:a copy", Guid.Empty,
            TranscodingJobType.Progressive, cancellation);

        receivedCommands.Should().ContainSingle().Which.Should().Be("-i input.flac -codec:a copy");
    }

    private static ReplayGainTranscodeManager CreateManager(ITranscodeManager inner, bool enabled) {
        var applicationPaths = new Mock<IApplicationPaths>();
        applicationPaths.Setup(paths => paths.DataPath).Returns(Path.GetTempPath());
        return new ReplayGainTranscodeManager(
            inner,
            NullLogger<ReplayGainTranscodeManager>.Instance,
            CreateEncodingHelper(applicationPaths.Object),
            new Mock<ILibraryManager>().Object,
            new LoudnormCacheStore(applicationPaths.Object, NullLogger<LoudnormCacheStore>.Instance),
            () => enabled);
    }

    private static EncodingHelper CreateEncodingHelper(IApplicationPaths applicationPaths) {
        return new EncodingHelper(
            applicationPaths,
            new Mock<IMediaEncoder>().Object,
            new Mock<ISubtitleEncoder>().Object,
            new ConfigurationBuilder().Build(),
            new Mock<MediaBrowser.Common.Configuration.IConfigurationManager>().Object,
            new Mock<IPathManager>().Object);
    }

    private static Mock<ITranscodeManager> CreateInner(out List<string> receivedCommands) {
        var commands = new List<string>();
        receivedCommands = commands;
        var inner = new Mock<ITranscodeManager>();
        inner
            .Setup(manager => manager.StartFfMpeg(
                It.IsAny<StreamState>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<TranscodingJobType>(),
                It.IsAny<CancellationTokenSource>(),
                It.IsAny<string?>()))
            .Callback<StreamState, string, string, Guid, TranscodingJobType, CancellationTokenSource, string?>((_, _,
                command, _, _, _, _) => commands.Add(command))
            .ReturnsAsync((TranscodingJob)null!);
        return inner;
    }

    private static StreamState CreateAudioState(ITranscodeManager inner) {
        return new StreamState(null!, TranscodingJobType.Progressive, inner) {
            BaseRequest = new BaseEncodingJobOptions(),
            AudioStream = new MediaStream { Channels = 2 },
            OutputAudioCodec = "aac"
        };
    }

}
