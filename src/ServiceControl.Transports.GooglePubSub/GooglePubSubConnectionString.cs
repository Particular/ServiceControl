namespace ServiceControl.Transports.GooglePubSub;

using System;
using System.Data.Common;
using System.Linq;

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
    }

    public string ProjectId { get; }
    public string ResourceNamePrefix { get; }
    public string ServiceEndpoint { get; }

    static string Read(DbConnectionStringBuilder builder, string key) =>
        builder.TryGetValue(key, out var value) && value is string text && !string.IsNullOrWhiteSpace(text) ? text : null;

    const string ProjectIdKey = "ProjectId";
    const string ResourceNamePrefixKey = "ResourceNamePrefix";
    const string ServiceEndpointKey = "ServiceEndpoint";

    static readonly string[] SupportedKeys = [ProjectIdKey, ResourceNamePrefixKey, ServiceEndpointKey];
}