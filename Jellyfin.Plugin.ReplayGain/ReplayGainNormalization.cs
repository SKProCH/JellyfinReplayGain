using MediaBrowser.Controller.Entities;

namespace Jellyfin.Plugin.ReplayGain;

internal static class ReplayGainNormalization
{
    public static float? GetEffectiveGain(BaseItem item)
    {
        var gain = item.LUFS.HasValue
            ? -18f - item.LUFS.Value
            : item.NormalizationGain;

        if (!gain.HasValue || !float.IsFinite(gain.Value) || gain.Value == 0)
        {
            return null;
        }

        return gain;
    }
}
