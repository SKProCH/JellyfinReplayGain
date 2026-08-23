namespace Jellyfin.Plugin.ReplayGain.Loudness.Models;

public sealed class LoudnessCache {
    public int Version { get; set; } = 1;

    public Dictionary<string, LoudnessFileResult> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
