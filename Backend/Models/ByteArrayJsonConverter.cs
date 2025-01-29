using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AssetItems.Models // Ensure the namespace matches your project's structure
{
    public class ByteArrayJsonConverter : JsonConverter<byte[]>
    {
        public override byte[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                string base64String = reader.GetString();
                return Convert.FromBase64String(base64String);
            }

            throw new JsonException("Invalid JSON value for byte[].");
        }

        public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(Convert.ToBase64String(value));
        }
    }
}
