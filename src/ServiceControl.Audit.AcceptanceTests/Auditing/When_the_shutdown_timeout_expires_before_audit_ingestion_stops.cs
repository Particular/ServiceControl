namespace ServiceControl.Audit.AcceptanceTests.Auditing
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Audit.Auditing;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    class When_the_shutdown_timeout_expires_before_audit_ingestion_stops : AcceptanceTest
    {
        [Test]
        public async Task Should_still_stop_the_ingestion_infrastructure()
        {
            SetSettings = settings => settings.ShutdownTimeout = TimeSpan.FromSeconds(1);
            CustomizeHostBuilder = builder => builder.Services.AddHostedService<ExhaustsShutdownTimeout>();

            var context = await Define<ScenarioContext>()
                .Done(c => HasLogged(c, AuditIngestion.LogMessages.StartedInfrastructure))
                .Run();

            Assert.That(HasLogged(context, AuditIngestion.LogMessages.StoppedInfrastructure), Is.True);
        }

        static bool HasLogged(ScenarioContext context, string message) =>
            context.Logs.ToArray().Any(log => log.Message.StartsWith(message));

        // Registered after ServiceControl, so the host stops it first
        class ExhaustsShutdownTimeout : IHostedService
        {
            public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

            public async Task StopAsync(CancellationToken cancellationToken = default) =>
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }
}
