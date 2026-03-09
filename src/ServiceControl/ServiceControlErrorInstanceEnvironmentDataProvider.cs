namespace Particular.ServiceControl;

using System.Collections.Generic;
using Particular.LicensingComponent.Contracts;
using ServiceBus.Management.Infrastructure.Settings;

class ServiceControlErrorInstanceEnvironmentDataProvider(Settings settings) : IEnvironmentDataProvider
{
    public IEnumerable<(string key, string value)> GetData()
    {
        yield return ("Features.IntegratedServicePulse", settings.EnableIntegratedServicePulse ? "Enabled" : "Disabled");
    }
}