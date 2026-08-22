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
        if (!ReplayGainPlugin.IsEnabled || !ReplayGainPlugin.Instance!.Configuration.UseLoudnorm) {
            return;
        }

        var query = new InternalItemsQuery {
            IncludeItemTypes = [BaseItemKind.Audio, BaseItemKind.Video],
            Recursive = true
        };
        var items = _libraryManager.GetItemList(query)
            .Where(item => item.IsFileProtocol && File.Exists(item.Path))
            .ToArray();

        for (var index = 0; index < items.Length; index++) {
            cancellationToken.ThrowIfCancellationRequested();
            await AnalyzeIfNeededAsync(items[index], cancellationToken).ConfigureAwait(false);
            progress.Report((index + 1) * 100d / Math.Max(items.Length, 1));
        }
    }

    private async Task AnalyzeIfNeededAsync(BaseItem item, CancellationToken cancellationToken) {
        var config = ReplayGainPlugin.Instance!.Configuration;
        var path = Path.GetFullPath(item.Path);
        FileSignature signature;
        try {
            signature = FileSignature.FromFile(path);
        }
        catch (IOException) {
            return;
        }

        var audioStreams = item.GetMediaStreams()
            .Where(stream => stream.Type == MediaStreamType.Audio)
            .ToArray();
        if (audioStreams.Length == 0 || _cache.TryGet(path, signature, config.LoudnormIntegratedLoudness,
                config.LoudnormTruePeak, config.LoudnormLoudnessRange, out _)) {
            return;
        }

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
            return;
        }

        for (var index = 0; index < results.Count; index++) {
            results[index].StreamIndex = audioStreams[index].Index;
        }

        try {
            var after = FileSignature.FromFile(path);
            if (after != signature) {
                _logger.LogInformation("Skipping loudnorm result because file changed during analysis: {Path}", path);
                return;
            }

            _cache.Put(path, signature, config.LoudnormIntegratedLoudness, config.LoudnormTruePeak,
                config.LoudnormLoudnessRange, results);
        }
        catch (IOException ex) {
            _logger.LogDebug(ex, "Could not save loudnorm result for {Path}", path);
        }

        return;

        string ComposeFilter(MediaStream _, int index) =>
            $"[0:a:{index}]loudnorm=I={Format(config.LoudnormIntegratedLoudness)}" +
            $":TP={Format(config.LoudnormTruePeak)}" +
            $":LRA={Format(config.LoudnormLoudnessRange)}" +
            $":print_format=json[norm{index}]";
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
}