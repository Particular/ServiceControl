namespace ServiceControl.Persistence.RavenDB;

using System;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Raven.Client.Documents;

static class LicenseStatusCheck
{
    record LicenseStatusFragment(string Id, string LicensedTo, string Status, bool Expired);

    public static async Task WaitForLicenseOrThrow(IDocumentStore documentStore, CancellationToken cancellationToken)
    {
        var ravenConfiguredHttpClient = documentStore.GetRequestExecutor().HttpClient;
        var licenseCheckUrl = documentStore.Urls[0].TrimEnd('/') + "/license/status";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(30_000);

        try
        {
            while (!cts.IsCancellationRequested)
            {
                var httpResponse = await ravenConfiguredHttpClient.GetAsync(licenseCheckUrl, cancellationToken);
                var licenseStatus = await httpResponse.Content.ReadFromJsonAsync<LicenseStatusFragment>(cancellationToken);
                if (licenseStatus.Expired)
                {
                    throw new InvalidOperationException("The current RavenDB license is expired. Please, contact support");
                }

                if (licenseStatus.LicensedTo != null && licenseStatus.Id != null)
                {
                    return;
                }

                await Task.Delay(200, cts.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("Cannot validate the current RavenDB license. Please, contact support");
        }
    }
}