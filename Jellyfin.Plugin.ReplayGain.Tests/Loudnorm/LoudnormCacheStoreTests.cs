using Jellyfin.Plugin.ReplayGain.Loudnorm;
using Jellyfin.Plugin.ReplayGain.Loudnorm.Models;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Jellyfin.Plugin.ReplayGain.Tests.Loudnorm;

public sealed class LoudnormCacheStoreTests
{
    [Fact]
    public void Constructor_WhenLegacyTargetOffsetExists_RemovesItFromCache()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var cachePath = Path.Combine(directory.FullName, "ReplayGain.loudnorm.json");
            File.WriteAllText(cachePath, "{\"Version\":1,\"Files\":{},\"target_offset\":0.45}");
            var paths = new Mock<IApplicationPaths>();
            paths.Setup(value => value.DataPath).Returns(directory.FullName);

            _ = new LoudnormCacheStore(paths.Object, NullLogger<LoudnormCacheStore>.Instance);

            File.ReadAllText(cachePath).Should().NotContain("target_offset");
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public void TryGet_WhenTargetsChange_ReusesMeasurement()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var filePath = Path.Combine(directory.FullName, "sample.mkv");
            File.WriteAllText(filePath, "sample");
            var paths = new Mock<IApplicationPaths>();
            paths.Setup(value => value.DataPath).Returns(directory.FullName);
            var store = new LoudnormCacheStore(paths.Object, NullLogger<LoudnormCacheStore>.Instance);
            var signature = FileSignature.FromFile(filePath);
            var streams = new[] { new AudioStreamSignature { Index = 1, Codec = "aac" } };
            store.Put(filePath, signature, streams, [new LoudnormStreamResult { StreamIndex = 1, InputI = -20 }]);

            store.TryGet(filePath, signature, streams, out _).Should().BeTrue();
        }
        finally
        {
            directory.Delete(true);
        }
    }
}
