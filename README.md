# ReplayGain Plugin

This separate Jellyfin plugin adds FFmpeg track ReplayGain normalization to audio transcoding.

- Disabled by default; enable it from the plugin configuration page.
- Direct Play and Remux playback are unaffected.
- ReplayGain is applied only when Jellyfin invokes FFmpeg to transcode audio.
- The FFmpeg build must support `volume=replaygain=track`.
- Track gain is used; album gain is not selected.

The source audio must contain ReplayGain metadata for FFmpeg to calculate a gain value. The plugin does not scan or rewrite media files.
