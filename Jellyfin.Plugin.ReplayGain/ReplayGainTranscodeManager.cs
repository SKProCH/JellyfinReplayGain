using System.Globalization;
using System.Text;
using Jellyfin.Plugin.ReplayGain.Loudnorm;
using Jellyfin.Plugin.ReplayGain.Loudnorm.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Streaming;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ReplayGain;

public sealed class ReplayGainTranscodeManager : ITranscodeManager {
    private readonly ITranscodeManager _inner;
    private readonly EncodingHelper _encodingHelper;
    private readonly Func<bool> _isEnabled;
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<ReplayGainTranscodeManager> _logger;
    private readonly LoudnormCacheStore _loudnormCache;

    public ReplayGainTranscodeManager(
        ITranscodeManager inner,
        ILogger<ReplayGainTranscodeManager> logger,
        EncodingHelper encodingHelper,
        ILibraryManager libraryManager,
        LoudnormCacheStore loudnormCache,
        Func<bool>? isEnabled = null) {
        _inner = inner;
        _logger = logger;
        _encodingHelper = encodingHelper;
        _isEnabled = isEnabled ?? (() => ReplayGainPlugin.IsEnabled);
        _libraryManager = libraryManager;
        _loudnormCache = loudnormCache;
    }

    public TranscodingJob? GetTranscodingJob(string playSessionId) {
        return _inner.GetTranscodingJob(playSessionId);
    }

    public TranscodingJob? GetTranscodingJob(string path, TranscodingJobType type) {
        return _inner.GetTranscodingJob(path, type);
    }

    public void PingTranscodingJob(string playSessionId, bool? isUserPaused) {
        _inner.PingTranscodingJob(playSessionId, isUserPaused);
    }

    public Task KillTranscodingJobs(string deviceId, string? playSessionId, Func<string, bool> deleteFiles) {
        return _inner.KillTranscodingJobs(deviceId, playSessionId, deleteFiles);
    }

    public void ReportTranscodingProgress(
        TranscodingJob job,
        StreamState state,
        TimeSpan? transcodingPosition,
        float? framerate,
        double? percentComplete,
        long? bytesTranscoded,
        int? bitRate) {
        _inner.ReportTranscodingProgress(job, state, transcodingPosition, framerate, percentComplete,
            bytesTranscoded, bitRate);
    }

    public Task<TranscodingJob> StartFfMpeg(
        StreamState state,
        string outputPath,
        string commandLineArguments,
        Guid userId,
        TranscodingJobType transcodingJobType,
        CancellationTokenSource cancellationTokenSource,
        string? workingDirectory = null) {
        var command = commandLineArguments;
        if (_isEnabled() && IsAudioTranscode(state)) {
            var filter = GetFilter(state);
            if (filter is not null) {
                if (EncodingHelper.IsCopyCodec(state.OutputAudioCodec)
                    && !ReplayGainCommandLine.TryReplaceAudioCopyCodec(command, GetAudioEncoder(state), out command)) {
                    _logger.LogWarning("ReplayGain could not replace the copied audio codec; using the original codec");
                }

                if (!ReplayGainCommandLine.TryAppendFilter(command, filter, out command)) {
                    _logger.LogWarning(
                        "ReplayGain could not safely update the FFmpeg command; using the original command line");
                }
            }
        }

        return _inner.StartFfMpeg(state, outputPath, command, userId, transcodingJobType, cancellationTokenSource,
            workingDirectory);
    }

    public void OnTranscodeEndRequest(TranscodingJob job) {
        _inner.OnTranscodeEndRequest(job);
    }

    public TranscodingJob? OnTranscodeBeginRequest(string path, TranscodingJobType type) {
        return _inner.OnTranscodeBeginRequest(path, type);
    }

    public ValueTask<IDisposable> LockAsync(string outputPath, CancellationToken cancellationToken) {
        return _inner.LockAsync(outputPath, cancellationToken);
    }

    private string? GetFilter(StreamState state) {
        var item = TryGetItem(state);
        if (item is not null
            && !string.IsNullOrWhiteSpace(state.MediaPath)
            && state.AudioStream is not null) {
            var config = ReplayGainPlugin.Instance!.Configuration;
            var signature = FileSignature.FromFile(state.MediaPath);
            var streamSignatures = item.GetMediaStreams()
                .Where(value => value.Type == MediaStreamType.Audio)
                .Select(value => new AudioStreamSignature {
                    Index = value.Index,
                    Codec = value.Codec,
                    Language = value.Language,
                    Channels = value.Channels,
                    SampleRate = value.SampleRate
                })
                .ToArray();
            if (_loudnormCache.TryGet(state.MediaPath, signature, streamSignatures,
                    config.LoudnormIntegratedLoudness, config.LoudnormTruePeak, config.LoudnormLoudnessRange,
                    out var result)) {
                var stream = result.Streams.FirstOrDefault(value => value.StreamIndex == state.AudioStream.Index);
                if (stream is not null) {
                    return $"loudnorm=I={Format(config.LoudnormIntegratedLoudness)}" +
                           $":TP={Format(config.LoudnormTruePeak)}" +
                           $":LRA={Format(config.LoudnormLoudnessRange)}" +
                           $":measured_I={Format(stream.InputI)}" +
                           $":measured_TP={Format(stream.InputTp)}" +
                           $":measured_LRA={Format(stream.InputLra)}" +
                           $":measured_thresh={Format(stream.InputThresh)}" +
                           $":offset={Format(stream.TargetOffset)}" +
                           $":linear=true";
                }
            }
        }

        return null;
    }

    private BaseItem? TryGetItem(StreamState state) {
        if (state.MediaSource is null || !Guid.TryParse(state.MediaSource.Id, out var id)) {
            return null;
        }

        return _libraryManager.GetItemById(id);
    }

    private static string Format(double value) {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private string GetAudioEncoder(StreamState state) {
        var temporaryState = new EncodingJobInfo(state.TranscodingType) {
            OutputAudioCodec = state.AudioStream?.Codec
        };
        return _encodingHelper.GetAudioEncoder(temporaryState);
    }

    internal static bool IsAudioTranscode(StreamState state) {
        if (state.BaseRequest?.Static == true) {
            return false;
        }

        return state.AudioStream is not null || !string.IsNullOrWhiteSpace(state.OutputAudioCodec);
    }

    public static class ReplayGainCommandLine {
        public static bool TryReplaceAudioCopyCodec(string commandLine, string encoder, out string updatedCommandLine) {
            updatedCommandLine = commandLine;
            var tokens = Tokenize(commandLine);
            if (tokens is null) {
                return false;
            }

            var replacements = new List<(int Start, int Length, string Value)>();
            for (var index = 0; index + 1 < tokens.Count; index++) {
                if (!IsAudioCodecOption(tokens[index].Value)
                    || !string.Equals(tokens[index + 1].Value, "copy", StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                var codecToken = tokens[index + 1];
                var original = commandLine.Substring(codecToken.Start, codecToken.Length);
                replacements.Add((codecToken.Start, codecToken.Length, QuoteLike(original, encoder)));
            }

            for (var index = replacements.Count - 1; index >= 0; index--) {
                var replacement = replacements[index];
                updatedCommandLine = updatedCommandLine[..replacement.Start]
                                     + replacement.Value
                                     + updatedCommandLine[(replacement.Start + replacement.Length)..];
            }

            return replacements.Count > 0;
        }

        public static bool TryAppendFilter(string commandLine, string filter, out string updatedCommandLine) {
            updatedCommandLine = commandLine;
            if (string.IsNullOrWhiteSpace(commandLine)) {
                return false;
            }

            var tokens = Tokenize(commandLine);
            if (tokens is null) {
                return false;
            }

            for (var i = 0; i < tokens.Count; i++) {
                if (!string.Equals(tokens[i].Value, "-af", StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                if (i + 1 >= tokens.Count || string.IsNullOrWhiteSpace(tokens[i + 1].Value)) {
                    return false;
                }

                var filterToken = tokens[i + 1];
                if (filterToken.Value.Contains(filter, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }

                var existing = filterToken.Value;
                var combined = existing + "," + filter;
                var original = commandLine.Substring(filterToken.Start, filterToken.Length);
                var replacement = original.Replace(existing, combined, StringComparison.Ordinal);
                updatedCommandLine = commandLine[..filterToken.Start] + replacement +
                                     commandLine[(filterToken.Start + filterToken.Length)..];
                return true;
            }

            var outputMarker = tokens.FirstOrDefault(token =>
                string.Equals(token.Value, "-y", StringComparison.OrdinalIgnoreCase));
            if (outputMarker is null) {
                return false;
            }

            updatedCommandLine = commandLine[..outputMarker.Start] + "-af \"" + filter + "\" " +
                                 commandLine[outputMarker.Start..];
            return true;
        }

        private static bool IsAudioCodecOption(string value) {
            if (value.StartsWith("-c:a", StringComparison.OrdinalIgnoreCase)) {
                return value.Length == 4 || value[4] == ':' && value.Length > 5;
            }

            if (value.StartsWith("-codec:a", StringComparison.OrdinalIgnoreCase)) {
                return value.Length == 8 || value[8] == ':' && value.Length > 9;
            }

            return false;
        }

        private static string QuoteLike(string original, string value) {
            if (original.Length >= 2
                && ((original[0] == '"' && original[^1] == '"')
                    || (original[0] == '\'' && original[^1] == '\''))) {
                return original[0] + value + original[^1];
            }

            return value;
        }

        private static List<Token>? Tokenize(string commandLine) {
            var tokens = new List<Token>();
            var index = 0;
            while (index < commandLine.Length) {
                while (index < commandLine.Length && char.IsWhiteSpace(commandLine[index])) {
                    index++;
                }

                if (index == commandLine.Length) {
                    break;
                }

                var start = index;
                var value = new StringBuilder();
                char? quote = null;
                while (index < commandLine.Length) {
                    var current = commandLine[index];
                    if (current == '\\' && index + 1 < commandLine.Length && quote == '"') {
                        value.Append(commandLine[index + 1]);
                        index += 2;
                        continue;
                    }

                    if (quote is null && (current == '\'' || current == '"')) {
                        quote = current;
                        index++;
                        continue;
                    }

                    if (quote is not null && current == quote) {
                        quote = null;
                        index++;
                        continue;
                    }

                    if (quote is null && char.IsWhiteSpace(current)) {
                        break;
                    }

                    value.Append(current);
                    index++;
                }

                if (quote is not null) {
                    return null;
                }

                tokens.Add(new Token(start, index - start, value.ToString()));
            }

            return tokens;
        }

        private sealed record Token(int Start, int Length, string Value);
    }
}
