namespace Jellyfin.Plugin.ReplayGain.Loudnorm.Models;

public sealed class LoudnormStreamResult {
    public int StreamIndex { get; set; }

    public double InputI { get; set; }

    public double InputTp { get; set; }

    public double InputLra { get; set; }

    public double InputThresh { get; set; }

    public double TargetOffset { get; set; }
}