namespace ServiceControl.Licensing
{
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.Auth;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Monitoring.HeartbeatMonitoring;
    using Particular.ServiceControl.Licensing;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.LicenseManagement;

    [ApiController]
    [Route("api")]
    public class LicenseController(ActiveLicense activeLicense, Settings settings, MassTransitConnectorHeartbeatStatus connectorHeartbeatStatus) : ControllerBase
    {
        [Authorize(Policy = Permissions.ErrorLicensingView)]
        [HttpGet]
        [Route("license")]
        public async Task<ActionResult<LicenseInfo>> License(bool refresh, string clientName, CancellationToken cancellationToken)
        {
            if (refresh)
            {
                await activeLicense.Refresh(cancellationToken);
            }

            var licenseInfo = new LicenseInfo
            {
                TrialLicense = activeLicense.Details.IsTrialLicense,
                Edition = activeLicense.Details.Edition ?? string.Empty,
                RegisteredTo = activeLicense.Details.RegisteredTo ?? string.Empty,
                UpgradeProtectionExpiration = activeLicense.Details.UpgradeProtectionExpiration?.ToString("O") ?? string.Empty,
                ExpirationDate = activeLicense.Details.ExpirationDate?.ToString("O") ?? string.Empty,
                Status = activeLicense.IsValid ? "valid" : "invalid",
                LicenseType = activeLicense.Details.LicenseType ?? string.Empty,
                InstanceName = settings.InstanceName ?? string.Empty,
                LicenseStatus = activeLicense.Details.Status,
                Products = activeLicense.Details.Products,
                LicenseExtensionUrl = connectorHeartbeatStatus.LastHeartbeat == null
                    ? $"https://particular.net/extend-your-trial?p={clientName}"
                    : $"https://particular.net/license/mt?p={clientName}&t={(activeLicense.IsEvaluation ? 0 : 1)}"
            };

            return licenseInfo;
        }

        [Authorize(Policy = Permissions.ErrorLicensingView)]
        [HttpGet]
        [Route("license/details")]
        public async Task<ActionResult<LicensedEndpointDetails>> LicenseDetails()
        {
            var fileContents = await System.IO.File.ReadAllTextAsync(@"C:\Projects\ServicePulse\src\Frontend\src\views\throughputreport\licenseDetails\sample.json");
            var result = JsonSerializer.Deserialize<LicensedEndpointDetails>(fileContents, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return result;
        }

        public class LicenseInfo
        {
            public bool TrialLicense { get; set; }

            public string Edition { get; set; }

            public string RegisteredTo { get; set; }

            public string UpgradeProtectionExpiration { get; set; }

            public string ExpirationDate { get; set; }

            public string Status { get; set; }

            public LicensedProduct[] Products { get; set; }

            public string LicenseType { get; set; }

            public string InstanceName { get; set; }

            public string LicenseStatus { get; set; }

            public string LicenseExtensionUrl { get; set; }
        }

        public class LicensedEndpointDetails
        {
            public LicensedEndpoint[] Endpoints { get; set; }
            public QueueIdentity[] InfrastructureQueues { get; set; }
            public QueueIdentity[] ExcludedQueues { get; set; }
            public string ServiceEndDate { get; set; }
            public Product[] Products { get; set; }
        }

        public class Product
        {
            public string ProductCode { get; set; }
            public int? MonthlyThroughput { get; set; }
        }

        public class QueueIdentity
        {
            public string NameHash { get; set; }
            public string Scope { get; set; }
        }

        public class LicensedEndpoint
        {
            public string Name { get; set; }
            public int Classification { get; set; }
            public string EndpointSize { get; set; }
            public QueueIdentity[] Queues { get; set; }
        }
    }
}