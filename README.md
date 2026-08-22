# ReplayGain Plugin

This separate Jellyfin plugin adds audio normalization to FFmpeg transcoding.

- Disabled by default; enable it from the plugin configuration page.
- Direct Play and Remux playback are unaffected.
- By default, the plugin uses Jellyfin's stored `NormalizationGain` value from its LUFS scan.
- Optionally, a scheduled background task can analyze all audio streams with two-pass `loudnorm`.
- Until a loudnorm result is available, Jellyfin's `NormalizationGain` remains the fallback.
- Direct Play and Remux playback are unaffected.

The plugin does not rewrite media files or ReplayGain tags. Loudnorm measurements are stored in Jellyfin's data directory as `ReplayGain.loudnorm.json`.
