using Jellyfin.Plugin.ReplayGain.Loudnorm;
using Jellyfin.Plugin.ReplayGain.Loudness.Models;
using Jellyfin.Plugin.ReplayGain.Configuration;
using Jellyfin.Plugin.ReplayGain.Loudness;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Jellyfin.Plugin.ReplayGain.Tests.Loudness;

public sealed class LoudnessCacheStoreTests
{
    [Fact]
    public void ParseEbur128Results_ParsesEachSummary() {
        const string output = """
            [Parsed_ebur128_0] Summary:
              Integrated loudness:
                I:         -24.2 LUFS
                Threshold: -34.8 LUFS

              Loudness range:
                LRA:        10.7 LU
                Threshold: -44.9 LUFS
                LRA low:   -31.4 LUFS
                LRA high:  -20.7 LUFS

              True peak:
                Peak:       -2.4 dBFS
            [Parsed_ebur128_1] Summary:
              Integrated loudness:
                I:         -23.8 LUFS
                Threshold: -34.1 LUFS

              Loudness range:
                LRA:         5.4 LU
                Threshold: -44.1 LUFS
                LRA low:   -26.8 LUFS
                LRA high:  -21.4 LUFS

              True peak:
                Peak:       -3.1 dBFS
            """;

        var results = LoudnormAnalyzer.ParseEbur128Results(output, 2);

        results.Should().NotBeNull();
        results![0].InputI.Should().BeApproximately(-24.2, 0.001);
        results[0].InputThresh.Should().BeApproximately(-34.8, 0.001);
        results[0].InputLra.Should().BeApproximately(10.7, 0.001);
        results[0].InputTp.Should().BeApproximately(-2.4, 0.001);
        results[1].InputI.Should().BeApproximately(-23.8, 0.001);
    }

    [Fact]
    public void ParseEbur128Results_UsesFinalSummaryWhenFfmpegPrintsInitialSummary() {
        const string output = """
            Integrated loudness:
              I:         -70.0 LUFS
              Threshold:   0.0 LUFS

            Loudness range:
              LRA:         0.0 LU
              Threshold:   0.0 LUFS

            True peak:
              Peak:       -inf dBFS

            Integrated loudness:
              I:         -21.1 LUFS
              Threshold: -31.1 LUFS

            Loudness range:
              LRA:         0.0 LU
              Threshold:   0.0 LUFS

            True peak:
              Peak:      -18.1 dBFS
            """;

        var results = LoudnormAnalyzer.ParseEbur128Results(output, 1);

        results.Should().NotBeNull();
        results![0].InputI.Should().BeApproximately(-21.1, 0.001);
        results[0].InputTp.Should().BeApproximately(-18.1, 0.001);
    }

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

            _ = new LoudnessCacheStore(paths.Object, NullLogger<LoudnessCacheStore>.Instance);

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
            var store = new LoudnessCacheStore(paths.Object, NullLogger<LoudnessCacheStore>.Instance);
            var signature = FileSignature.FromFile(filePath);
            var streams = new[] { new AudioStreamSignature { Index = 1, Codec = "aac" } };
            store.Put(filePath, signature, streams, [new LoudnessStreamResult { StreamIndex = 1, InputI = -20 }], MeasurementMethod.Loudnorm);

            store.TryGet(filePath, signature, streams, MeasurementMethod.Loudnorm, out _).Should().BeTrue();
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public void TryGet_WhenEbur128IsRequested_UsesLoudnormMeasurement()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var filePath = Path.Combine(directory.FullName, "sample.mkv");
            File.WriteAllText(filePath, "sample");
            var paths = new Mock<IApplicationPaths>();
            paths.Setup(value => value.DataPath).Returns(directory.FullName);
            var store = new LoudnessCacheStore(paths.Object, NullLogger<LoudnessCacheStore>.Instance);
            var signature = FileSignature.FromFile(filePath);
            var streams = new[] { new AudioStreamSignature { Index = 1, Codec = "aac" } };
            store.Put(filePath, signature, streams, [new LoudnessStreamResult { StreamIndex = 1 }], MeasurementMethod.Loudnorm);

            store.TryGet(filePath, signature, streams, MeasurementMethod.Ebur128, out _).Should().BeTrue();
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public void TryGet_WhenLoudnormIsRequested_DoesNotUseEbur128Measurement()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var filePath = Path.Combine(directory.FullName, "sample.mkv");
            File.WriteAllText(filePath, "sample");
            var paths = new Mock<IApplicationPaths>();
            paths.Setup(value => value.DataPath).Returns(directory.FullName);
            var store = new LoudnessCacheStore(paths.Object, NullLogger<LoudnessCacheStore>.Instance);
            var signature = FileSignature.FromFile(filePath);
            var streams = new[] { new AudioStreamSignature { Index = 1, Codec = "aac" } };
            store.Put(filePath, signature, streams, [new LoudnessStreamResult { StreamIndex = 1 }], MeasurementMethod.Ebur128);

            store.TryGet(filePath, signature, streams, MeasurementMethod.Loudnorm, out _).Should().BeFalse();
        }
        finally
        {
            directory.Delete(true);
        }
    }
}
