namespace ServiceControl.Transports.GooglePubSub;

using System;
using System.Data.Common;
using System.Linq;
using Google.Api.Gax;

class GooglePubSubConnectionString
{
    public GooglePubSubConnectionString(string connectionString)
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString ?? string.Empty };

        var unknownKeys = builder.Keys.Cast<string>().Where(key => !Array.Exists(SupportedKeys, supported => string.Equals(supported, key, StringComparison.OrdinalIgnoreCase))).ToArray();
        if (unknownKeys.Length > 0)
        {
            throw new ArgumentException($"The Google Pub/Sub connection string contains unsupported keys: {string.Join(", ", unknownKeys)}. Supported keys are {string.Join(", ", SupportedKeys)}. Credentials come from Application Default Credentials, not the connection string.");
        }

        ProjectId = Read(builder, ProjectIdKey);
        ResourceNamePrefix = Read(builder, ResourceNamePrefixKey);
        ServiceEndpoint = Read(builder, ServiceEndpointKey);

        if (Read(builder, EmulatorDetectionKey) is { } emulatorDetection)
        {
            if (!Enum.TryParse<EmulatorDetection>(emulatorDetection, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
            {
                throw new ArgumentException($"'{emulatorDetection}' is not a valid {EmulatorDetectionKey} value. Use one of {string.Join(", ", Enum.GetNames<EmulatorDetection>())}.");
            }

            EmulatorDetection = parsed;
        }
    }

    public string ProjectId { get; }
    public string ResourceNamePrefix { get; }
    public string ServiceEndpoint { get; }
    public EmulatorDetection? EmulatorDetection { get; }

    static string Read(DbConnectionStringBuilder builder, string key) =>
        builder.TryGetValue(key, out var value) && value is string text && !string.IsNullOrWhiteSpace(text) ? text : null;

    const string ProjectIdKey = "ProjectId";
    const string ResourceNamePrefixKey = "ResourceNamePrefix";
    const string ServiceEndpointKey = "ServiceEndpoint";
    const string EmulatorDetectionKey = "EmulatorDetection";

    static readonly string[] SupportedKeys = [ProjectIdKey, ResourceNamePrefixKey, ServiceEndpointKey, EmulatorDetectionKey];
}