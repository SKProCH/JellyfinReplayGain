using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.ReplayGain.Loudnorm.Models;

internal sealed class LoudnormJsonStatistics {
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
}
