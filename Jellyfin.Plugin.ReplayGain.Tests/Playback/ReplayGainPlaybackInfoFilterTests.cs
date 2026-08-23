using Jellyfin.Plugin.ReplayGain.Loudness;
using Jellyfin.Plugin.ReplayGain.Loudnorm;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Jellyfin.Plugin.ReplayGain.Tests.Playback;

public sealed class ReplayGainPlaybackInfoFilterTests
{
    [Fact]
    public void RequiresNormalization_WhenLoudnessCacheIsMissing_ReturnsFalse()
    {
        var source = CreateAudioSource();
        var filter = CreateFilter();

        filter.RequiresNormalization(source).Should().BeFalse();
    }

    [Fact]
    public void RequiresNormalization_WhenSourceHasNoPath_ReturnsFalse()
    {
        var source = CreateAudioSource();
        var filter = CreateFilter();

        filter.RequiresNormalization(source).Should().BeFalse();
    }

    [Fact]
    public void RequiresNormalization_WhenSourceHasNoAudio_ReturnsFalse()
    {
        var source = new MediaSourceInfo {
            MediaStreams = [new MediaStream { Type = MediaStreamType.Video }]
        };
        var filter = CreateFilter();

        filter.RequiresNormalization(source).Should().BeFalse();
    }

    [Fact]
    public void DirectPlayDecision_LeavesDirectStreamAvailable()
    {
        var source = CreateAudioSource();
        var filter = CreateFilter();

        filter.RequiresNormalization(source).Should().BeFalse();
        source.SupportsDirectPlay.Should().BeTrue();
        source.SupportsDirectStream.Should().BeTrue();
        source.SupportsTranscoding.Should().BeTrue();
    }

    private static ReplayGainPlaybackInfoFilter CreateFilter()
    {
        var applicationPaths = new Mock<IApplicationPaths>();
        applicationPaths.Setup(paths => paths.DataPath).Returns(Path.GetTempPath());
        return new ReplayGainPlaybackInfoFilter(
            new LoudnessCacheStore(applicationPaths.Object, NullLogger<LoudnessCacheStore>.Instance),
            NullLogger<ReplayGainPlaybackInfoFilter>.Instance);
    }

    private static MediaSourceInfo CreateAudioSource()
    {
        return new MediaSourceInfo {
            SupportsDirectPlay = true,
            SupportsDirectStream = true,
            SupportsTranscoding = true,
            MediaStreams = [new MediaStream { Type = MediaStreamType.Audio, Index = 0 }]
        };
    }
}
