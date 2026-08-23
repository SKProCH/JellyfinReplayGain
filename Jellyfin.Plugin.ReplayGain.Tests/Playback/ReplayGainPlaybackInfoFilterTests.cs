using Jellyfin.Plugin.ReplayGain.Loudnorm;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Jellyfin.Plugin.ReplayGain.Tests.Playback;

public sealed class ReplayGainPlaybackInfoFilterTests
{
    [Fact]
    public void RequiresNormalization_WhenTrackGainExists_ReturnsTrue()
    {
        var item = new Audio { NormalizationGain = -7.25f };
        var source = CreateAudioSource();
        var filter = CreateFilter();

        filter.RequiresNormalization(item, source).Should().BeTrue();
    }

    [Fact]
    public void RequiresNormalization_WhenNoGainAndLoudnormDisabled_ReturnsFalse()
    {
        var item = new Audio();
        var source = CreateAudioSource();
        var filter = CreateFilter();

        filter.RequiresNormalization(item, source).Should().BeFalse();
    }

    [Fact]
    public void RequiresNormalization_WhenSourceHasNoAudio_ReturnsFalse()
    {
        var item = new Audio { NormalizationGain = -7.25f };
        var source = new MediaSourceInfo {
            MediaStreams = [new MediaStream { Type = MediaStreamType.Video }]
        };
        var filter = CreateFilter();

        filter.RequiresNormalization(item, source).Should().BeFalse();
    }

    [Fact]
    public void DirectPlayDecision_LeavesDirectStreamAvailable()
    {
        var source = CreateAudioSource();
        var item = new Audio { NormalizationGain = -7.25f };
        var filter = CreateFilter();

        if (filter.RequiresNormalization(item, source))
        {
            source.SupportsDirectPlay = false;
        }

        source.SupportsDirectPlay.Should().BeFalse();
        source.SupportsDirectStream.Should().BeTrue();
        source.SupportsTranscoding.Should().BeTrue();
    }

    private static ReplayGainPlaybackInfoFilter CreateFilter()
    {
        var applicationPaths = new Mock<IApplicationPaths>();
        applicationPaths.Setup(paths => paths.DataPath).Returns(Path.GetTempPath());
        return new ReplayGainPlaybackInfoFilter(
            new Mock<MediaBrowser.Controller.Library.ILibraryManager>().Object,
            new LoudnormCacheStore(applicationPaths.Object, NullLogger<LoudnormCacheStore>.Instance),
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
