namespace ServiceControl.Audit.Persistence.Sql.Core.Infrastructure;

using System.Text.Json;

static class JsonSerializationOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
}
