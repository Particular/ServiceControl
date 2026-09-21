namespace ServiceControl.Migration.AcceptanceTests;

using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Hosting.Commands;

[TestFixture]
// Mandatory, not stylistic: this assembly is Parallelizable(ParallelScope.All) and these fixtures set
// process-global environment variables. One fixture added without it makes the whole suite intermittent.
[NonParallelizable]
class When_the_api_is_called_during_the_required_copy : MigrationAcceptanceTest
{
    [Test]
    public async Task The_api_does_not_answer_until_the_required_copy_has_finished()
    {
        await SeedSourceKnownEndpoints("Sales", "Billing", "Shipping");
        await SeedSourceEndpointSettings(("Sales", true), ("Billing", true), ("Shipping", true));

        var copyIsParked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseTheCopy = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var host = RunCommand.Run(Settings, AllowingAnIncompleteCategorySet(builder => builder.ParkFirstMigrationWrite(copyIsParked, releaseTheCopy.Task)), cancellation.Token);

        await copyIsParked.Task.WaitAsync(TimeSpan.FromMinutes(2));
        Assert.That(releaseTheCopy.Task.IsCompleted, Is.False, "the copy must still be parked for this assertion to mean anything");
        Assert.That(async () => await HttpClient.GetAsync(EndpointSettingsUrl), Throws.InstanceOf<HttpRequestException>(),
            "Kestrel had already bound its socket while the required copy was still running");

        releaseTheCopy.SetResult();
        var settings = await WaitForEndpointSettingsResponse(TimeSpan.FromMinutes(2));

        Assert.That(settings.Select(row => row.Name), Is.SupersetOf(new[] { "Sales", "Billing", "Shipping" }));

        await cancellation.CancelAsync();
        await host;
    }
}
