namespace Jellyfin.Plugin.ReplayGain.Loudnorm.Models;

public sealed class LoudnormFileResult {
    public long Length { get; set; }

    public DateTime LastWriteTimeUtc { get; set; }

    public double IntegratedLoudness { get; set; }

    public double TruePeak { get; set; }

    public double LoudnessRange { get; set; }

    public List<LoudnormStreamResult> Streams { get; set; } = [];
}