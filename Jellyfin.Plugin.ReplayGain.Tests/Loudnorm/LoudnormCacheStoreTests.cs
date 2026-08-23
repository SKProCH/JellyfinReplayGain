using Jellyfin.Plugin.ReplayGain.Loudnorm;
using Jellyfin.Plugin.ReplayGain.Loudnorm.Models;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Jellyfin.Plugin.ReplayGain.Tests.Loudnorm;

public sealed class LoudnormCacheStoreTests
{
    [Fact]
    public void TryGet_WhenTargetsChange_RequiresFreshAnalysisUnlessDynamicRangeIsPreserved()
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
            store.Put(filePath, signature, streams, -16, -1.5, 11, [new LoudnormStreamResult { StreamIndex = 1, InputI = -20 }]);

            store.TryGet(filePath, signature, streams, -14, -1.0, 8, false, out _).Should().BeFalse();
            store.TryGet(filePath, signature, streams, -14, -1.0, 8, true, out _).Should().BeTrue();
        }
        finally
        {
            directory.Delete(true);
        }
    }
}
