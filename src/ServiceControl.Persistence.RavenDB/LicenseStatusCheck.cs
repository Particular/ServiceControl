namespace ServiceControl.Persistence.RavenDB;

using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

static class LicenseStatusCheck
{
    record LicenseStatusFragment(string Id, string LicensedTo, string Status, bool Expired);

    public static async Task WaitForLicenseOrThrow(RavenPersisterSettings configuration, CancellationToken cancellationToken)
    {
        using var client = new HttpClient
        {
            BaseAddress = new Uri(configuration.ConnectionString ?? configuration.ServerUrl)
        };

        // Not linking to the incoming cancellationToken to ensure no OperationCancelledException prevents the last InvalidOperationException to be thrown
        using var cts = new CancellationTokenSource(30_000);
        while (!cts.IsCancellationRequested)
        {
            var httpResponse = await client.GetAsync("license/status", cancellationToken);
            var licenseStatus = await httpResponse.Content.ReadFromJsonAsync<LicenseStatusFragment>(cancellationToken);
            if (licenseStatus.Expired)
            {
                throw new InvalidOperationException("The current RavenDB license is expired. Please, contact support");
            }

            if (licenseStatus.LicensedTo != null && licenseStatus.Id != null)
            {
                return;
            }

            await Task.Delay(200, cancellationToken);
        }

        throw new InvalidOperationException("Cannot validate the current RavenDB license. Please, contact support");
    }
}