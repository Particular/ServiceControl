namespace ServiceControl.Mcp;

using System.Collections.Generic;
using Particular.LicensingComponent.Contracts;
using ServiceBus.Management.Infrastructure.Settings;

class McpEnvironmentDataProvider(Settings settings) : IEnvironmentDataProvider
{
    public IEnumerable<(string key, string value)> GetData()
    {
        yield return ("Features.Mcp", settings.EnableMcpServer ? "Enabled" : "Disabled");
        yield return ("Features.Mcp.WriteMode", settings.EnableMcpServerWriteMode ? "Enabled" : "Disabled");
    }
}
