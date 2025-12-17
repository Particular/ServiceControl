namespace ServiceControl.Persistence.RavenDB;

using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

static class LicenseStatusCheck
{
    class LicenseStatusFragment
    {
        public string Id { get; set; }
        public string LicensedTo { get; set; }
        public string Status { get; set; }
        public bool Expired { get; set; }
    }

    public static async Task WaitForLicenseOrThrow(RavenPersisterSettings configuration, CancellationToken cancellationToken)
    {
        var client = new HttpClient() { BaseAddress = new Uri(configuration.ConnectionString ?? configuration.ServerUrl) };
        var licenseCorrectlySetup = false;
        var attempts = 0;
        while (!licenseCorrectlySetup)
        {
            var httpResponse = await client.GetAsync("/license/status", cancellationToken);
            var responseJsonString = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            var licenseStatus = JsonSerializer.Deserialize<LicenseStatusFragment>(responseJsonString);
            if (licenseStatus.Expired)
            {
                throw new NotSupportedException("The current RavenDB license is expired. Please, contact support");
            }

            if (licenseStatus.LicensedTo != null && licenseStatus.Id != null)
            {
                licenseCorrectlySetup = true;
            }

            if (++attempts > 10)
            {
                throw new NotSupportedException("Cannot validate the current RavenDB license. Please, contact support");
            }

            await Task.Delay(500, cancellationToken);
        }
    }
}