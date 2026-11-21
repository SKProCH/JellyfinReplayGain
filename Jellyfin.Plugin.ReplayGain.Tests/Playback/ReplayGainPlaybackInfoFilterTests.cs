using Jellyfin.Plugin.ReplayGain.Configuration;
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
    public void RequiresNormalization_WhenLoudnessCacheIsMissing_UsesFallbackByDefault()
    {
        var path = Path.GetTempFileName();
        try {
            var source = CreateAudioSource(path);
            var filter = CreateFilter();

            filter.RequiresNormalization(source).Should().BeTrue();
        }
        finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void RequiresNormalization_WhenLoudnessCacheIsMissingAndDynamicRangeIsPreserved_ReturnsFalse()
    {
        var path = Path.GetTempFileName();
        try {
            var source = CreateAudioSource(path);
            var filter = CreateFilter(preserveDynamicRange: true);

            filter.RequiresNormalization(source).Should().BeFalse();
        }
        finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void RequiresNormalization_WhenLoudnessCacheIsMissingAndFallbackIsDisabled_ReturnsFalse()
    {
        var path = Path.GetTempFileName();
        try {
            var source = CreateAudioSource(path);
            var filter = CreateFilter(normalizeBeforeAnalysis: false);

            filter.RequiresNormalization(source).Should().BeFalse();
        }
        finally {
            File.Delete(path);
        }
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

    private static ReplayGainPlaybackInfoFilter CreateFilter(bool preserveDynamicRange = false, bool normalizeBeforeAnalysis = true)
    {
        var applicationPaths = new Mock<IApplicationPaths>();
        applicationPaths.Setup(paths => paths.DataPath).Returns(Path.GetTempPath());
        return new ReplayGainPlaybackInfoFilter(
            new LoudnessCacheStore(applicationPaths.Object, NullLogger<LoudnessCacheStore>.Instance),
            NullLogger<ReplayGainPlaybackInfoFilter>.Instance,
            () => new PluginConfiguration {
                PreserveDynamicRange = preserveDynamicRange,
                NormalizeBeforeAnalysis = normalizeBeforeAnalysis
            });
    }

    private static MediaSourceInfo CreateAudioSource(string? path = null)
    {
        return new MediaSourceInfo {
            Path = path,
            SupportsDirectPlay = true,
            SupportsDirectStream = true,
            SupportsTranscoding = true,
            MediaStreams = [new MediaStream { Type = MediaStreamType.Audio, Index = 0 }]
        };
    }
}
