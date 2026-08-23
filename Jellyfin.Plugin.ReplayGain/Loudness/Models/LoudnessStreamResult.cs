namespace Jellyfin.Plugin.ReplayGain.Loudness.Models;

public sealed class LoudnessStreamResult {
    public int StreamIndex { get; set; }

    public double InputI { get; set; }

    public double InputTp { get; set; }

    public double InputLra { get; set; }

    public double InputThresh { get; set; }

}
