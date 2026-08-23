using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.ReplayGain.Loudnorm.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ReplayGain.Loudnorm;

public sealed class LoudnormAnalyzer : IScheduledTask {
    private readonly LoudnormCacheStore _cache;
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<LoudnormAnalyzer> _logger;
    private readonly IMediaEncoder _mediaEncoder;

    public LoudnormAnalyzer(
        ILibraryManager libraryManager,
        IMediaEncoder mediaEncoder,
        LoudnormCacheStore cache,
        ILogger<LoudnormAnalyzer> logger) {
        _libraryManager = libraryManager;
        _mediaEncoder = mediaEncoder;
        _cache = cache;
        _logger = logger;
    }

    public string Name {
        get => "ReplayGain loudnorm analysis";
    }

    public string Description {
        get => "Analyzes audio streams for two-pass loudnorm playback.";
    }

    public string Category {
        get => "Library";
    }

    public string Key {
        get => "ReplayGainLoudnormAnalysis";
    }

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() {
        yield return new TaskTriggerInfo {
            Type = TaskTriggerInfoType.IntervalTrigger,
            IntervalTicks = TimeSpan.FromHours(24).Ticks
        };
    }

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken) {
        if (!ReplayGainPlugin.IsEnabled) {
            _logger.LogInformation("ReplayGain loudnorm analysis skipped because the plugin is disabled");
            return;
        }

        var query = new InternalItemsQuery {
            MediaTypes = [MediaType.Audio, MediaType.Video],
            Recursive = true
        };
        var indexedItems = _libraryManager.GetItemList(query);
        var items = indexedItems
            .Where(item => item.IsFileProtocol && File.Exists(item.Path))
            .ToArray();
        var skippedUnavailable = indexedItems.Count - items.Length;
        var summary = new AnalysisSummary();

        _logger.LogInformation(
            "ReplayGain loudnorm analysis started: {IndexedItemCount} audio/video item(s), {CandidateCount} local file(s), {UnavailableCount} unavailable or non-file item(s), target I {TargetI} LUFS, TP {TargetTp} dBTP, LRA {TargetLra} LU, preserve dynamic range {PreserveDynamicRange}",
            indexedItems.Count, items.Length, skippedUnavailable, ReplayGainPlugin.Instance!.Configuration.LoudnormIntegratedLoudness,
            ReplayGainPlugin.Instance.Configuration.LoudnormTruePeak, ReplayGainPlugin.Instance.Configuration.LoudnormLoudnessRange,
            ReplayGainPlugin.Instance.Configuration.PreserveDynamicRange);

        for (var index = 0; index < items.Length; index++) {
            cancellationToken.ThrowIfCancellationRequested();
            summary.Add(await AnalyzeIfNeededAsync(items[index], cancellationToken).ConfigureAwait(false));
            progress.Report((index + 1) * 100d / Math.Max(items.Length, 1));
        }

        _logger.LogInformation(
            "ReplayGain loudnorm analysis completed: {AnalyzedCount} analyzed, {CachedCount} cached, {NoAudioStreamCount} without audio streams, {UnreadableCount} unreadable, {FailedCount} failed, {ChangedCount} changed during analysis, {CacheWriteFailedCount} cache write failures",
            summary.Analyzed, summary.Cached, summary.NoAudioStreams, summary.Unreadable, summary.Failed,
            summary.Changed, summary.CacheWriteFailed);
    }

    private async Task<AnalysisOutcome> AnalyzeIfNeededAsync(BaseItem item, CancellationToken cancellationToken) {
        var config = ReplayGainPlugin.Instance!.Configuration;
        var path = Path.GetFullPath(item.Path);
        FileSignature signature;
        try {
            signature = FileSignature.FromFile(path);
        }
        catch (IOException) {
            return AnalysisOutcome.Unreadable;
        }

        var audioStreams = item.GetMediaStreams()
            .Where(stream => stream.Type == MediaStreamType.Audio)
            .ToArray();
        var streamSignatures = audioStreams
            .Select(CreateSignature)
            .ToArray();
        if (audioStreams.Length == 0) {
            return AnalysisOutcome.NoAudioStreams;
        }

        if (_cache.TryGet(path, signature, streamSignatures,
                config.LoudnormIntegratedLoudness, config.LoudnormTruePeak, config.LoudnormLoudnessRange,
                config.PreserveDynamicRange, out _)) {
            return AnalysisOutcome.Cached;
        }

        _logger.LogDebug(
            "Starting loudnorm analysis for {Path}: {StreamCount} audio stream(s), target I {TargetI} LUFS, TP {TargetTp} dBTP, LRA {TargetLra} LU, preserve dynamic range {PreserveDynamicRange}",
            path, audioStreams.Length, config.LoudnormIntegratedLoudness, config.LoudnormTruePeak,
            config.LoudnormLoudnessRange, config.PreserveDynamicRange);

        var filters = audioStreams.Select(ComposeFilter);
        var arguments = new List<string>
            { "-hide_banner", "-i", path, "-filter_complex", string.Join(';', filters), "-vn", "-sn" };
        foreach (var index in Enumerable.Range(0, audioStreams.Length)) {
            arguments.AddRange(["-map", $"[norm{index}]"]);
        }

        arguments.AddRange(["-f", "null", OperatingSystem.IsWindows() ? "NUL" : "/dev/null"]);
        var output = await RunAsync(arguments, cancellationToken).ConfigureAwait(false);
        var results = ParseResults(output, audioStreams.Length);
        if (results is null) {
            _logger.LogWarning("Could not parse {ExpectedCount} loudnorm result(s) for {Path}", audioStreams.Length, path);
            return AnalysisOutcome.Failed;
        }

        for (var index = 0; index < results.Count; index++) {
            results[index].StreamIndex = audioStreams[index].Index;
        }

        try {
            var after = FileSignature.FromFile(path);
            if (after != signature) {
                _logger.LogInformation("Skipping loudnorm result because file changed during analysis: {Path}", path);
                return AnalysisOutcome.Changed;
            }

            _cache.Put(path, signature, streamSignatures, config.LoudnormIntegratedLoudness,
                config.LoudnormTruePeak, config.LoudnormLoudnessRange, results);
            _logger.LogDebug("Saved loudnorm analysis for {Path}: {StreamCount} audio stream(s)", path, results.Count);
            return AnalysisOutcome.Analyzed;
        }
        catch (IOException ex) {
            _logger.LogDebug(ex, "Could not save loudnorm result for {Path}", path);
            return AnalysisOutcome.CacheWriteFailed;
        }

        string ComposeFilter(MediaStream _, int index) =>
            $"[0:a:{index}]loudnorm=I={Format(config.LoudnormIntegratedLoudness)}" +
            $":TP={Format(config.LoudnormTruePeak)}" +
            $":LRA={Format(config.LoudnormLoudnessRange)}" +
            $":print_format=json[norm{index}]";

        static AudioStreamSignature CreateSignature(MediaStream stream) => new() {
            Index = stream.Index,
            Codec = stream.Codec,
            Language = stream.Language,
            Channels = stream.Channels,
            SampleRate = stream.SampleRate
        };
    }

    private async Task<string> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken) {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo {
            FileName = _mediaEncoder.EncoderPath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) {
            process.StartInfo.ArgumentList.Add(argument);
        }

        _logger.LogDebug("Starting loudnorm analysis with {Encoder}", _mediaEncoder.EncoderPath);
        process.Start();
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var text = await error.ConfigureAwait(false) + await output.ConfigureAwait(false);
        if (process.ExitCode != 0) {
            _logger.LogWarning("loudnorm analysis failed with exit code {ExitCode}", process.ExitCode);
        }

        return text;
    }

    internal static List<LoudnormStreamResult>? ParseResults(string output, int expectedCount) {
        var results = new List<LoudnormStreamResult>();
        var start = 0;
        while ((start = output.IndexOf('{', start)) >= 0) {
            var depth = 0;
            var inString = false;
            var escaped = false;
            var end = -1;
            for (var index = start; index < output.Length; index++) {
                var character = output[index];
                if (escaped) {
                    escaped = false;
                    continue;
                }

                if (character == '\\' && inString) {
                    escaped = true;
                }
                else if (character == '"') {
                    inString = !inString;
                }
                else if (!inString && character == '{') {
                    depth++;
                }
                else if (!inString && character == '}' && --depth == 0) {
                    end = index + 1;
                    break;
                }
            }

            if (end < 0) {
                break;
            }

            try {
                var statistics = JsonSerializer.Deserialize<LoudnormJsonStatistics>(output[start..end]);
                if (statistics is not null) {
                    results.Add(new LoudnormStreamResult {
                        StreamIndex = results.Count,
                        InputI = statistics.InputI,
                        InputTp = statistics.InputTp,
                        InputLra = statistics.InputLra,
                        InputThresh = statistics.InputThresh,
                        TargetOffset = statistics.TargetOffset
                    });
                }
            }
            catch (JsonException) {
                // FFmpeg logs may contain unrelated JSON-like text.
            }

            start = end;
        }

        return results.Count == expectedCount ? results : null;
    }

    private static string Format(double value) {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private enum AnalysisOutcome {
        Analyzed,
        Cached,
        NoAudioStreams,
        Unreadable,
        Failed,
        Changed,
        CacheWriteFailed
    }

    private sealed class AnalysisSummary {
        public int Analyzed { get; private set; }
        public int Cached { get; private set; }
        public int NoAudioStreams { get; private set; }
        public int Unreadable { get; private set; }
        public int Failed { get; private set; }
        public int Changed { get; private set; }
        public int CacheWriteFailed { get; private set; }

        public void Add(AnalysisOutcome outcome) {
            switch (outcome) {
                case AnalysisOutcome.Analyzed:
                    Analyzed++;
                    break;
                case AnalysisOutcome.Cached:
                    Cached++;
                    break;
                case AnalysisOutcome.NoAudioStreams:
                    NoAudioStreams++;
                    break;
                case AnalysisOutcome.Unreadable:
                    Unreadable++;
                    break;
                case AnalysisOutcome.Failed:
                    Failed++;
                    break;
                case AnalysisOutcome.Changed:
                    Changed++;
                    break;
                case AnalysisOutcome.CacheWriteFailed:
                    CacheWriteFailed++;
                    break;
            }
        }
    }
}
