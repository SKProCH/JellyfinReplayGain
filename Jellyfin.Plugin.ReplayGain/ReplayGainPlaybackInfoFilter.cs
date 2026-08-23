using Jellyfin.Plugin.ReplayGain.Loudnorm;
using Jellyfin.Plugin.ReplayGain.Loudnorm.Models;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ReplayGain;

public sealed class ReplayGainPlaybackInfoFilter(
    LoudnormCacheStore loudnormCache,
    ILogger<ReplayGainPlaybackInfoFilter> logger) : IAsyncActionFilter {
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next) {
        var executedContext = await next().ConfigureAwait(false);
        if (!ReplayGainPlugin.IsEnabled || !TryGetPlaybackInfo(executedContext.Result, out var playbackInfo)) {
            return;
        }

        if (!Guid.TryParse(Convert.ToString(context.RouteData.Values["itemId"]), out var itemId)) {
            return;
        }

        foreach (var mediaSource in playbackInfo.MediaSources) {
            if (!mediaSource.SupportsDirectPlay) {
                logger.LogDebug(
                    "ReplayGain matched PlaybackInfo for item {ItemId}, source {SourceId}, but did not change it: direct play is already disabled",
                    itemId, mediaSource.Id);
                continue;
            }

            if (mediaSource is { SupportsDirectStream: false, SupportsTranscoding: false }) {
                logger.LogDebug(
                    "ReplayGain matched PlaybackInfo for item {ItemId}, source {SourceId}, but did not change it: direct stream and transcoding are unavailable",
                    itemId, mediaSource.Id);
                continue;
            }

            if (!RequiresNormalization(mediaSource, out var reason)) {
                logger.LogDebug(
                    "ReplayGain matched PlaybackInfo for item {ItemId}, source {SourceId}, but did not change it: {Reason}",
                    itemId, mediaSource.Id, reason);
                continue;
            }

            mediaSource.SupportsDirectPlay = false;
            logger.LogInformation(
                "ReplayGain disabled direct play for item {ItemId}, source {SourceId}, path {Path} to allow loudness adjustment",
                itemId, mediaSource.Id, mediaSource.Path);
        }
    }

    internal bool RequiresNormalization(MediaSourceInfo mediaSource) {
        return RequiresNormalization(mediaSource, out _);
    }

    private bool RequiresNormalization(MediaSourceInfo mediaSource, out string reason) {
        if (mediaSource.MediaStreams.All(stream => stream.Type != MediaStreamType.Audio)) {
            reason = "the source has no audio stream";
            return false;
        }

        if (string.IsNullOrWhiteSpace(mediaSource.Path)) {
            reason = "the source has no media path";
            return false;
        }

        var config = ReplayGainPlugin.Instance!.Configuration;
        var signatures = mediaSource.MediaStreams
            .Where(stream => stream.Type == MediaStreamType.Audio)
            .Select(stream => new AudioStreamSignature {
                Index = stream.Index,
                Codec = stream.Codec,
                Language = stream.Language,
                Channels = stream.Channels,
                SampleRate = stream.SampleRate
            })
            .ToArray();
        var signature = FileSignature.FromFile(mediaSource.Path);
        if (!loudnormCache.TryGet(mediaSource.Path, signature, signatures,
                config.LoudnormIntegratedLoudness, config.LoudnormTruePeak, config.LoudnormLoudnessRange,
                config.PreserveDynamicRange, out var result)) {
            reason = "no matching loudnorm cache entry exists";
            return false;
        }

        var selectedIndex = mediaSource.DefaultAudioStreamIndex ?? signatures.FirstOrDefault()?.Index;
        var stream = selectedIndex.HasValue
            ? result.Streams.FirstOrDefault(value => value.StreamIndex == selectedIndex.Value)
            : null;
        if (stream is null) {
            reason = $"the loudnorm cache has no result for audio stream {selectedIndex?.ToString() ?? "<none>"}";
            return false;
        }

        if (!config.PreserveDynamicRange) {
            reason = string.Empty;
            return true;
        }

        var gain = ReplayGainTranscodeManager.CalculatePeakSafeGain(
            config.LoudnormIntegratedLoudness,
            config.LoudnormTruePeak,
            stream.InputI,
            stream.InputTp);
        if (double.IsFinite(gain) && gain != 0) {
            reason = string.Empty;
            return true;
        }

        reason = $"constant-gain mode produced no finite non-zero gain ({gain} dB)";
        return false;
    }

    private static bool TryGetPlaybackInfo(IActionResult? actionResult, out PlaybackInfoResponse playbackInfo) {
        playbackInfo = null!;
        if (actionResult is not ObjectResult { Value: PlaybackInfoResponse value }) {
            return false;
        }

        playbackInfo = value;
        return value.ErrorCode is null;
    }
}