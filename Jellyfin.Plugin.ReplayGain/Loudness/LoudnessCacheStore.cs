using System.Text.Json;
using Jellyfin.Plugin.ReplayGain.Configuration;
using Jellyfin.Plugin.ReplayGain.Loudness.Models;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

// ReSharper disable CompareOfFloatsByEqualityOperator

namespace Jellyfin.Plugin.ReplayGain.Loudness;

public sealed class LoudnessCacheStore {
    private const string FileName = "ReplayGain.loudnorm.json";
    private readonly object _gate = new();
    private readonly ILogger<LoudnessCacheStore> _logger;
    private readonly string _path;
    private readonly LoudnessCache _cache;

    public LoudnessCacheStore(IApplicationPaths applicationPaths, ILogger<LoudnessCacheStore> logger) {
        _path = Path.Combine(applicationPaths.DataPath, FileName);
        _logger = logger;
        _cache = Load();
    }

    public bool TryGet(string path, FileSignature signature, IReadOnlyList<AudioStreamSignature> audioStreams,
        MeasurementMethod measurementMethod,
        out LoudnessFileResult result) {
        var key = Path.GetFullPath(path);
        lock (_gate) {
            if (_cache.Files.TryGetValue(key, out result!)
                && result.Length == signature.Length
                && result.LastWriteTimeUtc == signature.LastWriteTimeUtc
                && IsUsableFor(result.MeasurementMethod ?? MeasurementMethod.Loudnorm, measurementMethod)
                && result.AudioStreams.SequenceEqual(audioStreams)) {
                return true;
            }
        }

        result = null!;
        return false;
    }

    public bool TryGetAny(string path, FileSignature signature, IReadOnlyList<AudioStreamSignature> audioStreams,
        out LoudnessFileResult result) {
        var key = Path.GetFullPath(path);
        lock (_gate) {
            if (_cache.Files.TryGetValue(key, out result!)
                && result.Length == signature.Length
                && result.LastWriteTimeUtc == signature.LastWriteTimeUtc
                && result.AudioStreams.SequenceEqual(audioStreams)) {
                return true;
            }
        }

        result = null!;
        return false;
    }

    private static bool IsUsableFor(MeasurementMethod storedMethod, MeasurementMethod requestedMethod) {
        return storedMethod == requestedMethod
            || requestedMethod == MeasurementMethod.Ebur128 && storedMethod == MeasurementMethod.Loudnorm;
    }

    public void Put(string path, FileSignature signature, IReadOnlyList<AudioStreamSignature> audioStreams,
        IReadOnlyList<LoudnessStreamResult> streams, MeasurementMethod measurementMethod) {
        var key = Path.GetFullPath(path);
        lock (_gate) {
            _cache.Files[key] = new LoudnessFileResult {
                MeasurementMethod = measurementMethod,
                Length = signature.Length,
                LastWriteTimeUtc = signature.LastWriteTimeUtc,
                AudioStreams = audioStreams.ToList(),
                Streams = streams.ToList()
            };
            Save(_cache);
        }
    }

    private LoudnessCache Load() {
        try {
            if (File.Exists(_path)) {
                var text = File.ReadAllText(_path);
                var cache = JsonSerializer.Deserialize<LoudnessCache>(text) ?? new LoudnessCache();
                if (text.Contains("\"target_offset\"", StringComparison.OrdinalIgnoreCase)) {
                    Save(cache);
                }

                return cache;
            }
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "Could not load loudnorm cache {Path}", _path);
        }

        return new LoudnessCache();
    }

    private void Save(LoudnessCache cache) {
        var directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = _path + ".tmp";
        File.WriteAllText(temporaryPath,
            JsonSerializer.Serialize(cache, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporaryPath, _path, true);
    }
}
