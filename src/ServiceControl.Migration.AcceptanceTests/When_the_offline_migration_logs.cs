namespace ServiceControl.Migration.AcceptanceTests;

using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using ServiceControl.Hosting.Commands;
using ServiceControl.Infrastructure;

[TestFixture]
[NonParallelizable]
class When_the_offline_migration_logs : MigrationAcceptanceTest
{
    [Test]
    public void It_logs_only_through_the_providers_the_instance_is_configured_with()
    {
        string[] registered = null;

        Assert.ThrowsAsync<ProvidersCaptured>(async () => await MigrateCommand.Run(Settings, builder =>
        {
            registered = ProviderTypes(builder.Services);
            throw new ProvidersCaptured();
        }));

        var configured = ProviderTypes(new ServiceCollection().AddLogging(logging => logging.ConfigureLogging(Settings.LoggingSettings.LogLevel)));

        Assert.That(registered, Is.EquivalentTo(configured),
            "an extra default provider prints every line twice and prints EF Core's SQL, which the configured providers filter out");
    }

    static string[] ProviderTypes(IServiceCollection services) =>
        [.. services
            .Where(service => service.ServiceType == typeof(ILoggerProvider))
            .Select(service => (service.ImplementationType ?? service.ImplementationInstance?.GetType() ?? service.ImplementationFactory?.Method.ReturnType)?.FullName ?? "unknown")];

    sealed class ProvidersCaptured : Exception;
}
