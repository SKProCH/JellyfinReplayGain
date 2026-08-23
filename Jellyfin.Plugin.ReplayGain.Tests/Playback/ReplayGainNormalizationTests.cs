using MediaBrowser.Controller.Entities.Audio;

namespace Jellyfin.Plugin.ReplayGain.Tests.Playback;

public sealed class ReplayGainNormalizationTests
{
    [Fact]
    public void GetEffectiveGain_WhenLufsExists_UsesJellyfinLufsFormula()
    {
        var item = new Audio { LUFS = -14.5f, NormalizationGain = -3f };

        ReplayGainNormalization.GetEffectiveGain(item).Should().BeApproximately(-3.5f, 0.001f);
    }

    [Fact]
    public void GetEffectiveGain_WhenLufsIsMissing_UsesReplayGainMetadata()
    {
        var item = new Audio { NormalizationGain = -7.25f };

        ReplayGainNormalization.GetEffectiveGain(item).Should().BeApproximately(-7.25f, 0.001f);
    }

    [Fact]
    public void GetEffectiveGain_WhenGainIsZero_ReturnsNull()
    {
        var item = new Audio { NormalizationGain = 0f };

        ReplayGainNormalization.GetEffectiveGain(item).Should().BeNull();
    }

    [Fact]
    public void GetEffectiveGain_WhenLufsMatchesReferenceLevel_ReturnsNull()
    {
        var item = new Audio { LUFS = -18f, NormalizationGain = -3f };

        ReplayGainNormalization.GetEffectiveGain(item).Should().BeNull();
    }
}
