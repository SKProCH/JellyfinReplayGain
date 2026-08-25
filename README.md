# Jellyfin ReplayGain Plugin

This plugin for Jellyfin normalizes audio loudness during FFmpeg transcoding.  
It analyzes audio streams in the background and uses cached loudness measurements to adjust loudness during playback.  

Find this plugin useful? Consider starring this repository.

## Why?

Movies and shows can have significantly different perceived loudness levels. This often requires adjusting the volume when switching between media items.

Moreover, some clients can't boost volume over 100%, e.g. browser jellyfin and [smart tv clients](https://github.com/Moonfin-Client/Smart-TV/issues/265). So this force users to watch a very queit videos.

This plugin allows to measure proper loudness and readjust it while encoding.

## Installation

### Requirements

- **Jellyfin 10.11.x**
- Local media files accessible to the Jellyfin server for background analysis

### Via Plugin Repository (recommended)

1. In Jellyfin, go to **Dashboard -> Plugins -> Repositories**
2. Add a new repository with the URL:
   ```
   https://skproch.github.io/JellyfinReplayGain/manifest.json
   ```
3. Go to **Catalog**, find **ReplayGain** and install it
4. Restart your Jellyfin Server

### Manual Installation

1. Download the latest plugin zip from [GitHub Releases](https://github.com/SKProCH/JellyfinReplayGain/releases)
2. Extract the zip into a new folder inside your Jellyfin server's `plugins` directory, e.g. `<Jellyfin Data Folder>/plugins/ReplayGain/`
3. Restart your Jellyfin Server

## Features

- Doesn't alter the *dynamic range* of your videos, only adjusting the overall volume  
  <sup>Except when the requested gain would exceed the configured true peak target; FFmpeg then falls back to dynamic normalization to prevent clipping</sup>
- Provides an optional **Preserve dynamic range** mode using a peak-safe constant gain  
  <sup>If you are absulutely don't want the dynamic range altering</sup>
- Analyzes every audio stream in local video files using FFmpeg
- Supports `ebur128` (fast measurement) and `loudnorm` (native two-pass measurement)
- Runs analysis after a library scan, when the plugin is enabled, and through a scheduled task
- Supports configurable integrated loudness, true peak, and loudness range targets
- Uses the measured values in the second-pass `loudnorm` filter; constant-gain mode remains available to preserve dynamic range
- Does not modify media files or write ReplayGain tags

## Important technical things

> [!IMPORTANT]
> Jellyfin cannot apply server-side audio filters during Direct Play. After a valid measurement is available, this plugin disables Direct Play for the selected source and adds normalization when Jellyfin transcodes its audio.  
> This means that **ALL your streams** (that needs loudness adjustments) **will be reencoded** to adjust your audio loudness. Video can still be copied without re-encoding (when supported).

- The loudnorm analysis is slow, somewhat CPU heavy, and single-threaded, so processing a large media library may take a considerable amount of time
- Normalization is applied before Jellyfin's device-specific audio filters, such as channel downmixing and downmix boost. This keeps the cached analysis independent of the playback device.

## My other plugins

[JellyfinStreamGenerator](https://github.com/SKProCH/JellyfinStreamGenerator) - adds a "Generate Stream URL" option to the context menu for choosing the video and audio codecs, streams, and subtitle options, etc

## Versioning

Plugin versions use the format `x.y.z.N`. The last digit (`N`) is the preview build number - `0` indicates a stable release.

## Building from source

```bash
dotnet build
```

The compiled library will be available at `Jellyfin.Plugin.ReplayGain/bin/Debug/net9.0/Jellyfin.Plugin.ReplayGain.dll`.
