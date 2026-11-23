namespace ServiceControl.Migration.AcceptanceTests;

using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Hosting.Commands;

[TestFixture]
[NonParallelizable]
class When_the_api_is_called_during_the_required_copy : MigrationAcceptanceTest
{
    const string HostOpenedSetting = "Migration/HostOpenedOnTarget";

    [Test]
    public async Task The_api_opens_only_after_the_offline_command_finishes_and_the_operator_starts_ServiceControl()
    {
        await SeedSourceKnownEndpoints("Sales", "Billing", "Shipping");
        await SeedSourceEndpointSettings(("Sales", true), ("Billing", true), ("Shipping", true));

        var copyIsParked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseTheCopy = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var copy = MigrateCommand.Run(Settings, builder => builder.ParkFirstMigrationWrite(copyIsParked, releaseTheCopy.Task), cancellation.Token);

        try
        {
            await copyIsParked.Task.WaitAsync(TimeSpan.FromMinutes(2));
            Assert.That(async () => await HttpClient.GetAsync(EndpointSettingsUrl), Throws.InstanceOf<HttpRequestException>());
            Assert.That(async () => await RunCommand.Run(Settings, null, cancellation.Token),
                Throws.Exception.With.Message.Contains("ServiceControl will not start"));
            Assert.That(await ReadSetting(HostOpenedSetting), Is.Null, "the abort is still free while the offline copy runs");
        }
        finally
        {
            releaseTheCopy.TrySetResult();
            await copy;
        }

        await ExpectExitCode(copy);

        Assert.That(async () => await HttpClient.GetAsync(EndpointSettingsUrl), Throws.InstanceOf<HttpRequestException>(),
            "the offline command must not start ServiceControl itself");
        Assert.That(await ReadSetting(HostOpenedSetting), Is.Null, "the offline command never opens the service");

        await RunHostUntilTheApiAnswers();
        var settings = await GetEndpointSettings();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings.Select(row => row.Name), Is.SupersetOf(new[] { "Sales", "Billing", "Shipping" }));
            Assert.That(await ReadSetting(HostOpenedSetting), Is.Not.Null, "ServiceControl has opened on the target, so the free abort is over");
        }
    }
}
