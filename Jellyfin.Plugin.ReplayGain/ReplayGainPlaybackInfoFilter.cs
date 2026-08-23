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
    ILogger<ReplayGainPlaybackInfoFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executedContext = await next().ConfigureAwait(false);
        if (!ReplayGainPlugin.IsEnabled || !TryGetPlaybackInfo(executedContext.Result, out var playbackInfo))
        {
            return;
        }

        if (!Guid.TryParse(Convert.ToString(context.RouteData.Values["itemId"]), out var itemId))
        {
            return;
        }

        var changed = false;
        foreach (var mediaSource in playbackInfo.MediaSources)
        {
            if (!mediaSource.SupportsDirectPlay
                || mediaSource is { SupportsDirectStream: false, SupportsTranscoding: false }
                || !RequiresNormalization(mediaSource))
            {
                continue;
            }

            mediaSource.SupportsDirectPlay = false;
            changed = true;
        }

        if (changed)
        {
            logger.LogDebug("Disabled direct play for item {ItemId} because ReplayGain normalization is available", itemId);
        }
    }

    internal bool RequiresNormalization(MediaSourceInfo mediaSource)
    {
        if (mediaSource.MediaStreams.All(stream => stream.Type != MediaStreamType.Audio))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(mediaSource.Path))
        {
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
            if (loudnormCache.TryGet(mediaSource.Path, signature, signatures,
                    config.LoudnormIntegratedLoudness, config.LoudnormTruePeak, config.LoudnormLoudnessRange,
                    out var result))
            {
                var selectedIndex = mediaSource.DefaultAudioStreamIndex
                    ?? signatures.FirstOrDefault()?.Index;
                if (selectedIndex.HasValue
                    && result.Streams.Any(stream => stream.StreamIndex == selectedIndex.Value))
                {
                    var stream = result.Streams.First(stream => stream.StreamIndex == selectedIndex.Value);
                    return !config.PreserveDynamicRange
                        || double.IsFinite(config.LoudnormIntegratedLoudness - stream.InputI)
                            && config.LoudnormIntegratedLoudness - stream.InputI != 0;
                }
            }
        }

        return false;
    }

    private static bool TryGetPlaybackInfo(IActionResult? actionResult, out PlaybackInfoResponse playbackInfo)
    {
        playbackInfo = null!;
        if (actionResult is not ObjectResult { Value: PlaybackInfoResponse value })
        {
            return false;
        }

        playbackInfo = value;
        return value.ErrorCode is null;
    }
}
