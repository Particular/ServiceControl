#nullable enable
namespace ServiceControl.Licensing
{
    using System;
    using System.IO;
    using System.IO.Compression;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.Auth;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Monitoring.HeartbeatMonitoring;
    using Particular.LicensingComponent.Contracts;
    using Particular.LicensingComponent.Persistence;
    using Particular.ServiceControl.Licensing;
    using ServiceBus.Management.Infrastructure.Settings;

    [ApiController]
    [Route("api")]
    public class LicenseController(ActiveLicense activeLicense, Settings settings, MassTransitConnectorHeartbeatStatus connectorHeartbeatStatus, ILicensingDataStore dataStore) : ControllerBase
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
        public async Task<ActionResult<LicensedEndpointDetails?>> LicenseDetails(CancellationToken cancellationToken)
        {
            return await dataStore.GetLicensedEndpointDetails(cancellationToken);
        }

        [Authorize(Policy = Permissions.ErrorLicensingManage)]
        [HttpPost]
        [Route("license/detailsUpload")]
        public async Task UploadLicenseDetails([FromForm] IFormFile file, CancellationToken cancellationToken)
        {
            //perform date and license id checks
            using var fileStream = file.OpenReadStream();
            using var fileStreamReader = new StreamReader(fileStream);
            var compressed = await fileStreamReader.ReadToEndAsync(cancellationToken);
            var compressedData = Convert.FromBase64String(compressed);
            using var memoryStream = new MemoryStream(compressedData);
            using var brotliStream = new BrotliStream(memoryStream, CompressionMode.Decompress);
            using var reader = new StreamReader(brotliStream, Encoding.UTF8);

            var fileContents = reader.ReadToEnd();
            var result = JsonSerializer.Deserialize<LicensedEndpointDetails>(fileContents, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("File contents cannot be deserialized");
            //persist
            await dataStore.SaveLicensedEndpointDetails(result, cancellationToken);
        }
    }
}