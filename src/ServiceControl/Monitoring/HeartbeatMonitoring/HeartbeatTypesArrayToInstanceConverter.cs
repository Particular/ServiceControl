#nullable enable

namespace ServiceControl.Monitoring.HeartbeatMonitoring
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Plugin.Heartbeat.Messages;
    using ServiceControl.HeartbeatMonitoring.InternalMessages;

    // ServiceControl.Plugin.Nsb5.Heartbeat used to send RegisterEndpointStartup and EndpointHeartbeat messages wrapped in an array.
    // In order to stay backward compatible, we need to convert the array to an instance.
    public class HeartbeatTypesArrayToInstanceConverter : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(RegisterEndpointStartup) ||
                                                               typeToConvert == typeof(EndpointHeartbeat) ||
                                                               typeToConvert == typeof(RegisterPotentiallyMissingHeartbeats);

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
            typeToConvert switch
            {
                _ when typeToConvert == typeof(RegisterEndpointStartup) => new Converter<RegisterEndpointStartup>(options),
                _ when typeToConvert == typeof(EndpointHeartbeat) => new Converter<EndpointHeartbeat>(options),
                _ when typeToConvert == typeof(RegisterPotentiallyMissingHeartbeats) => new Converter<RegisterPotentiallyMissingHeartbeats>(options),
                _ => throw new NotSupportedException()
            };

        sealed class Converter<TValue>(JsonSerializerOptions options) : JsonConverter<TValue>
        {
            // Currently we want to rely on deserializing the actual value by using the standard json serializer
            // To make sure we are not recursively invoking the converter we need to remove it from the options
            // Removing from the options is currently only possible when the converter is added to the options directly
            // and not supported when the converter is added to the type
            readonly JsonSerializerOptions optionsWithoutCustomConverter = options.FromWithout<HeartbeatTypesArrayToInstanceConverter>();

            public override TValue? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                var readArray = false;
                if (reader.TokenType == JsonTokenType.StartArray)
                {
                    readArray = true;
                    reader.Read();

                    if (reader.TokenType != JsonTokenType.StartObject)
                    {
                        throw new JsonException();
                    }
                }

                var value = JsonSerializer.Deserialize<TValue>(ref reader, optionsWithoutCustomConverter);

                if (!readArray)
                {
                    return value;
                }

                if (reader.TokenType != JsonTokenType.EndObject)
                {
                    throw new JsonException();
                }

                _ = reader.Read();

                if (reader.TokenType != JsonTokenType.EndArray)
                {
                    throw new JsonException();
                }

                _ = reader.Read();

                return value;
            }

            // we only ever use it to read
            public override void Write(Utf8JsonWriter writer, TValue value, JsonSerializerOptions options) =>
                throw new NotImplementedException();
        }
    }

    static class JsonSerializerOptionsExtensions
    {
        public static JsonSerializerOptions FromWithout<TConverter>(this JsonSerializerOptions options)
            where TConverter : JsonConverter
        {
            var newOptions = new JsonSerializerOptions(options);
            JsonConverter? converterToRemove = null;
            foreach (var converter in newOptions.Converters)
            {
                if (converter is not TConverter)
                {
                    continue;
                }

                converterToRemove = converter;
                break;
            }

            if (converterToRemove != null)
            {
                newOptions.Converters.Remove(converterToRemove);
            }

            return newOptions;
        }
    }
}