namespace ServiceControl.Infrastructure.WebApi
{
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.Json.Serialization.Metadata;

    public static class SerializerOptions
    {
        public static readonly JsonSerializerOptions Default = new JsonSerializerOptions().CustomizeDefaults();

        public static JsonSerializerOptions CustomizeDefaults(this JsonSerializerOptions options)
        {
            options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.WriteIndented = false;
            options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            options.TypeInfoResolver = (options.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver()).WithAddedModifier(DoNotEnforceRequiredMembers);
            return options;
        }

        // C# 'required' members document what our own code must set when it creates these types. The JSON
        // we read comes from other instances, often of other versions, that write with WhenWritingNull (as
        // above), so a value they do not know is simply absent. System.Text.Json enforces 'required' on
        // deserialization, and one such message would fail an audit instance's whole response, dropping it
        // from the scatter-gather.
        static void DoNotEnforceRequiredMembers(JsonTypeInfo typeInfo)
        {
            foreach (var property in typeInfo.Properties)
            {
                property.IsRequired = false;
            }
        }
    }
}