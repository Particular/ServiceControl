#nullable enable
namespace ServiceControl.Licensing
{
    using System.IO;
    using System.IO.Compression;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.Auth;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Particular.LicensingComponent.Contracts;
    using Particular.LicensingComponent.Persistence;
    using Particular.ServiceControl.Licensing;

    [ApiController]
    [Route("api")]
    public class LicenseController(ActiveLicense activeLicense, ILicenseInfoProvider licenseInfoProvider, ILicensingDataStore dataStore) : ControllerBase
    {
        [Authorize(Policy = Permissions.ErrorLicensingView)]
        [HttpGet]
        [Route("license")]
        public async Task<ActionResult<LicenseInfo>> License(bool refresh, string clientName, CancellationToken cancellationToken = default) =>
            await licenseInfoProvider.GetLicense(refresh, clientName, cancellationToken);

        [Authorize(Policy = Permissions.ErrorThroughputView)]
        [HttpGet]
        [Route("license/details")]
        public async Task<ActionResult<LicensedEndpointDetails?>> LicenseDetails(CancellationToken cancellationToken = default)
        {
            if (activeLicense.Details.Edition != "Endpoint Size" || !activeLicense.Details.HasEndpointMetadata)
            {
                return (LicensedEndpointDetails?)null;
            }

            var licenseDetails = await dataStore.GetLicensedEndpointDetails(cancellationToken);
            if (licenseDetails is null)
            {
                return (LicensedEndpointDetails?)null;
            }

            licenseDetails.ValidId = licenseDetails.LicenseId == activeLicense.Details.Id;
            return licenseDetails;
        }

        [Authorize(Policy = Permissions.ErrorThroughputManage)]
        [HttpPost]
        [Route("license/detailsUpload")]
        public async Task UploadLicenseDetails([FromForm] IFormFile file, CancellationToken cancellationToken = default)
        {
            //perform date and license id checks
            using var brotliStream = new BrotliStream(file.OpenReadStream(), CompressionMode.Decompress);

            var result = JsonSerializer.Deserialize<LicensedEndpointDetails>(brotliStream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("File contents cannot be deserialized");
            //persist
            await dataStore.SaveLicensedEndpointDetails(result, cancellationToken);
        }
    }
}