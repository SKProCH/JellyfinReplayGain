namespace Jellyfin.Plugin.ReplayGain.Loudness.Models;

using Jellyfin.Plugin.ReplayGain.Configuration;

public sealed class LoudnessFileResult {
    // Null identifies cache entries written before measurement methods were added.
    public MeasurementMethod? MeasurementMethod { get; set; }

    public long Length { get; set; }

    public DateTime LastWriteTimeUtc { get; set; }

    public List<AudioStreamSignature> AudioStreams { get; set; } = [];

    public List<LoudnessStreamResult> Streams { get; set; } = [];
}
