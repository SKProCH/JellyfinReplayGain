using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.ReplayGain.Loudnorm;

internal sealed class LoudnormJsonStatistics
{
    [JsonPropertyName("input_i")]
    [JsonConverter(typeof(LoudnormNumberConverter))]
    public double InputI { get; set; }

    [JsonPropertyName("input_tp")]
    [JsonConverter(typeof(LoudnormNumberConverter))]
    public double InputTp { get; set; }

    [JsonPropertyName("input_lra")]
    [JsonConverter(typeof(LoudnormNumberConverter))]
    public double InputLra { get; set; }

    [JsonPropertyName("input_thresh")]
    [JsonConverter(typeof(LoudnormNumberConverter))]
    public double InputThresh { get; set; }

    [JsonPropertyName("target_offset")]
    [JsonConverter(typeof(LoudnormNumberConverter))]
    public double TargetOffset { get; set; }
}

internal sealed class LoudnormNumberConverter : JsonConverter<double>
{
    public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetDouble().ToString(CultureInfo.InvariantCulture),
            JsonTokenType.String => reader.GetString()!,
            _ => throw new JsonException($"Expected a number or string, got {reader.TokenType}.")
        };

        if (string.Equals(value, "-inf", StringComparison.OrdinalIgnoreCase))
        {
            return -99;
        }

        if (string.Equals(value, "inf", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return double.Parse(value, CultureInfo.InvariantCulture);
    }

    public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value);
}

public sealed class LoudnormCache
{
    public int Version { get; set; } = 1;

    public Dictionary<string, LoudnormFileResult> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class LoudnormFileResult
{
    public long Length { get; set; }

    public DateTime LastWriteTimeUtc { get; set; }

    public double IntegratedLoudness { get; set; }

    public double TruePeak { get; set; }

    public double LoudnessRange { get; set; }

    public List<LoudnormStreamResult> Streams { get; set; } = [];
}

public sealed class LoudnormStreamResult
{
    public int StreamIndex { get; set; }

    public double InputI { get; set; }

    public double InputTp { get; set; }

    public double InputLra { get; set; }

    public double InputThresh { get; set; }

    public double TargetOffset { get; set; }
}

public readonly record struct FileSignature(long Length, DateTime LastWriteTimeUtc)
{
    public static FileSignature FromFile(string path)
    {
        var info = new FileInfo(path);
        return new FileSignature(info.Length, info.LastWriteTimeUtc);
    }
}
