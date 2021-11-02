namespace NServiceBus
{
    using System;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    class JsonTimeSpanConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) =>
            typeToConvert == typeof(TimeSpan)
            || (typeToConvert.IsGenericType && Nullable.GetUnderlyingType(typeToConvert) == typeof(TimeSpan));

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
            typeToConvert.IsGenericType
                ? (JsonConverter)new NullableJsonTimeSpanConverter()
                : new JsonTimeSpanConverter();

        class JsonTimeSpanConverter : JsonConverter<TimeSpan>
        {
            public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
                TimeSpan.ParseExact(reader.GetString(), "c", CultureInfo.CurrentCulture);

            public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options) =>
                throw new NotImplementedException();
        }

        class NullableJsonTimeSpanConverter : JsonConverter<TimeSpan?>
        {
            public override TimeSpan? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                var s = reader.GetString();
                return s == null ? (TimeSpan?)null : TimeSpan.ParseExact(s, "c", CultureInfo.CurrentCulture);
            }

            public override void Write(Utf8JsonWriter writer, TimeSpan? value, JsonSerializerOptions options) => throw new NotImplementedException();
        }
    }
}