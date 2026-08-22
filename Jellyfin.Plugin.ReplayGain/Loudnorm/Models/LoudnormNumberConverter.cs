using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.ReplayGain.Loudnorm.Models;

internal sealed class LoudnormNumberConverter : JsonConverter<double> {
    public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        var value = reader.TokenType switch {
            JsonTokenType.Number => reader.GetDouble().ToString(CultureInfo.InvariantCulture),
            JsonTokenType.String => reader.GetString()!,
            _ => throw new JsonException($"Expected a number or string, got {reader.TokenType}.")
        };

        if (string.Equals(value, "-inf", StringComparison.OrdinalIgnoreCase)) {
            return -99;
        }

        if (string.Equals(value, "inf", StringComparison.OrdinalIgnoreCase)) {
            return 0;
        }

        return double.Parse(value, CultureInfo.InvariantCulture);
    }

    public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options) {
        writer.WriteNumberValue(value);
    }
}