using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qomicex.Core.AOT.Utils
{
    /// <summary>
    /// 专门给Fabric Json的神秘Time做的转换器
    /// </summary>
    internal class MinecraftDateTimeConverter : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var raw = reader.GetString();
            if (string.IsNullOrEmpty(raw))
                throw new JsonException("Invalid datetime");

            if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
                return result;

            if (raw.Length >= 6)
            {
                int idx = raw.Length - 5;
                if (raw[idx] is '+' or '-')
                {
                    raw = string.Concat(raw.AsSpan(0, idx + 3), ":", raw.AsSpan(idx + 3));
                    if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
                        return result;
                }
            }

            throw new JsonException($"Cannot parse datetime: {raw}");
        }

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString("yyyy-MM-dd'T'HH:mm:ssK"));
        }
    }
}
