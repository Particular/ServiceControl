namespace ServiceControl.Audit.Persistence.RavenDB;

using System;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Raven.Client.Documents;

static class LicenseStatusCheck
{
    record LicenseStatusFragment(string Id, string LicensedTo, string Status, bool Expired);

    public static async Task WaitForLicenseOrThrow(IDocumentStore documentStore, CancellationToken cancellationToken = default)
    {
        var ravenConfiguredHttpClient = documentStore.GetRequestExecutor().HttpClient;
        var licenseCheckUrl = documentStore.Urls[0].TrimEnd('/') + "/license/status";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(30_000);

        try
        {
            while (true)
            {
                var httpResponse = await ravenConfiguredHttpClient.GetAsync(licenseCheckUrl, cts.Token);
                var licenseStatus = await httpResponse.Content.ReadFromJsonAsync<LicenseStatusFragment>(cts.Token);
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
#pragma warning disable PS0020 // The try runs on the linked cts.Token, which is always cancelled in the timeout case this maps. Filtering on it instead of the caller's token would make the filter unreachable
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
#pragma warning restore PS0020
        {
            throw new InvalidOperationException("Cannot validate the current RavenDB license. Please, contact support");
        }
    }
}
