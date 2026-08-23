using System.Text.Json;
using Jellyfin.Plugin.ReplayGain.Loudnorm.Models;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;
// ReSharper disable CompareOfFloatsByEqualityOperator

namespace Jellyfin.Plugin.ReplayGain.Loudnorm;

public sealed class LoudnormCacheStore {
    private const string FileName = "ReplayGain.loudnorm.json";
    private readonly object _gate = new();
    private readonly ILogger<LoudnormCacheStore> _logger;
    private readonly string _path;
    private readonly LoudnormCache _cache;

    public LoudnormCacheStore(IApplicationPaths applicationPaths, ILogger<LoudnormCacheStore> logger) {
        _path = Path.Combine(applicationPaths.DataPath, FileName);
        _logger = logger;
        _cache = Load();
    }

    public bool TryGet(string path, FileSignature signature, IReadOnlyList<AudioStreamSignature> audioStreams,
        out LoudnormFileResult result) {
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

    public void Put(string path, FileSignature signature, IReadOnlyList<AudioStreamSignature> audioStreams,
        IReadOnlyList<LoudnormStreamResult> streams) {
        var key = Path.GetFullPath(path);
        lock (_gate) {
            _cache.Files[key] = new LoudnormFileResult {
                Length = signature.Length,
                LastWriteTimeUtc = signature.LastWriteTimeUtc,
                AudioStreams = audioStreams.ToList(),
                Streams = streams.ToList()
            };
            Save(_cache);
        }
    }

    private LoudnormCache Load() {
        try {
            if (File.Exists(_path)) {
                var text = File.ReadAllText(_path);
                var cache = JsonSerializer.Deserialize<LoudnormCache>(text) ?? new LoudnormCache();
                if (text.Contains("\"target_offset\"", StringComparison.OrdinalIgnoreCase)) {
                    Save(cache);
                }

                return cache;
            }
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "Could not load loudnorm cache {Path}", _path);
        }

        return new LoudnormCache();
    }

    private void Save(LoudnormCache cache) {
        var directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = _path + ".tmp";
        File.WriteAllText(temporaryPath,
            JsonSerializer.Serialize(cache, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporaryPath, _path, true);
    }
}
