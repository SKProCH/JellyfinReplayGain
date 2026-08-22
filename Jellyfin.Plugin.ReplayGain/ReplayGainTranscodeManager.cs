using System.Text;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Streaming;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ReplayGain;

public sealed class ReplayGainTranscodeManager : ITranscodeManager
{
    private const string ReplayGainFilter = "volume=replaygain=track";
    private readonly ITranscodeManager _inner;
    private readonly ILogger<ReplayGainTranscodeManager> _logger;
    private readonly Func<bool> _isEnabled;

    public ReplayGainTranscodeManager(
        ITranscodeManager inner,
        ILogger<ReplayGainTranscodeManager> logger,
        Func<bool>? isEnabled = null)
    {
        _inner = inner;
        _logger = logger;
        _isEnabled = isEnabled ?? (() => ReplayGainPlugin.IsEnabled);
    }

    public TranscodingJob? GetTranscodingJob(string playSessionId) => _inner.GetTranscodingJob(playSessionId);

    public TranscodingJob? GetTranscodingJob(string path, TranscodingJobType type) => _inner.GetTranscodingJob(path, type);

    public void PingTranscodingJob(string playSessionId, bool? isUserPaused) => _inner.PingTranscodingJob(playSessionId, isUserPaused);

    public Task KillTranscodingJobs(string deviceId, string? playSessionId, Func<string, bool> deleteFiles)
        => _inner.KillTranscodingJobs(deviceId, playSessionId, deleteFiles);

    public void ReportTranscodingProgress(
        TranscodingJob job,
        StreamState state,
        TimeSpan? transcodingPosition,
        float? framerate,
        double? percentComplete,
        long? bytesTranscoded,
        int? bitRate)
        => _inner.ReportTranscodingProgress(job, state, transcodingPosition, framerate, percentComplete, bytesTranscoded, bitRate);

    public Task<TranscodingJob> StartFfMpeg(
        StreamState state,
        string outputPath,
        string commandLineArguments,
        Guid userId,
        TranscodingJobType transcodingJobType,
        CancellationTokenSource cancellationTokenSource,
        string? workingDirectory = null)
    {
        var command = commandLineArguments;
        if (_isEnabled() && IsAudioTranscode(state))
        {
            if (!ReplayGainCommandLine.TryAppend(commandLineArguments, out command))
            {
                _logger.LogWarning("ReplayGain could not safely update the FFmpeg command; using the original command line");
            }
        }

        return _inner.StartFfMpeg(state, outputPath, command, userId, transcodingJobType, cancellationTokenSource, workingDirectory);
    }

    public void OnTranscodeEndRequest(TranscodingJob job) => _inner.OnTranscodeEndRequest(job);

    public TranscodingJob? OnTranscodeBeginRequest(string path, TranscodingJobType type)
        => _inner.OnTranscodeBeginRequest(path, type);

    public ValueTask<IDisposable> LockAsync(string outputPath, CancellationToken cancellationToken)
        => _inner.LockAsync(outputPath, cancellationToken);

    internal static bool IsAudioTranscode(StreamState state)
    {
        if (state.BaseRequest?.Static == true || EncodingHelper.IsCopyCodec(state.OutputAudioCodec))
        {
            return false;
        }

        return state.AudioStream is not null || !string.IsNullOrWhiteSpace(state.OutputAudioCodec);
    }

    public static class ReplayGainCommandLine
    {
        public static bool TryAppend(string commandLine, out string updatedCommandLine)
        {
            updatedCommandLine = commandLine;
            if (string.IsNullOrWhiteSpace(commandLine))
            {
                return false;
            }

            var tokens = Tokenize(commandLine);
            if (tokens is null)
            {
                return false;
            }

            for (var i = 0; i < tokens.Count; i++)
            {
                if (!string.Equals(tokens[i].Value, "-af", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (i + 1 >= tokens.Count || string.IsNullOrWhiteSpace(tokens[i + 1].Value))
                {
                    return false;
                }

                var filterToken = tokens[i + 1];
                if (filterToken.Value.Contains("replaygain=track", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                var existing = filterToken.Value;
                var combined = existing + "," + ReplayGainFilter;
                var original = commandLine.Substring(filterToken.Start, filterToken.Length);
                var replacement = original.Replace(existing, combined, StringComparison.Ordinal);
                updatedCommandLine = commandLine[..filterToken.Start] + replacement + commandLine[(filterToken.Start + filterToken.Length)..];
                return true;
            }

            var outputMarker = tokens.FirstOrDefault(token => string.Equals(token.Value, "-y", StringComparison.OrdinalIgnoreCase));
            if (outputMarker is null)
            {
                return false;
            }

            updatedCommandLine = commandLine[..outputMarker.Start] + "-af \"" + ReplayGainFilter + "\" " + commandLine[outputMarker.Start..];
            return true;
        }

        private static List<Token>? Tokenize(string commandLine)
        {
            var tokens = new List<Token>();
            var index = 0;
            while (index < commandLine.Length)
            {
                while (index < commandLine.Length && char.IsWhiteSpace(commandLine[index]))
                {
                    index++;
                }

                if (index == commandLine.Length)
                {
                    break;
                }

                var start = index;
                var value = new StringBuilder();
                char? quote = null;
                while (index < commandLine.Length)
                {
                    var current = commandLine[index];
                    if (current == '\\' && index + 1 < commandLine.Length && quote == '"')
                    {
                        value.Append(commandLine[index + 1]);
                        index += 2;
                        continue;
                    }

                    if (quote is null && (current == '\'' || current == '"'))
                    {
                        quote = current;
                        index++;
                        continue;
                    }

                    if (quote is not null && current == quote)
                    {
                        quote = null;
                        index++;
                        continue;
                    }

                    if (quote is null && char.IsWhiteSpace(current))
                    {
                        break;
                    }

                    value.Append(current);
                    index++;
                }

                if (quote is not null)
                {
                    return null;
                }

                tokens.Add(new Token(start, index - start, value.ToString()));
            }

            return tokens;
        }

        private sealed record Token(int Start, int Length, string Value);
    }
}
