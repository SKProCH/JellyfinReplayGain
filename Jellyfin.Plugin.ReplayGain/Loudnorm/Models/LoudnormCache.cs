namespace Jellyfin.Plugin.ReplayGain.Loudnorm.Models;

public sealed class LoudnormCache {
    public int Version { get; set; } = 1;

    public Dictionary<string, LoudnormFileResult> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}