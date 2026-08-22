namespace Jellyfin.Plugin.ReplayGain.Loudnorm.Models;

public sealed class AudioStreamSignature
{
    public int Index { get; set; }

    public string? Codec { get; set; }

    public string? Language { get; set; }

    public int? Channels { get; set; }

    public int? SampleRate { get; set; }

    public override bool Equals(object? obj)
    {
        return obj is AudioStreamSignature other
            && Index == other.Index
            && string.Equals(Codec, other.Codec, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Language, other.Language, StringComparison.OrdinalIgnoreCase)
            && Channels == other.Channels
            && SampleRate == other.SampleRate;
    }

    public override int GetHashCode()
        => HashCode.Combine(Index, Codec?.ToUpperInvariant(), Language?.ToUpperInvariant(), Channels, SampleRate);
}
