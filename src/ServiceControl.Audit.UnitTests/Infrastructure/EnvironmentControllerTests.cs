namespace ServiceControl.Audit.UnitTests.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging.Abstractions;
    using NUnit.Framework;
    using ServiceControl.Audit.Infrastructure.WebApi;
    using ServiceControl.Audit.Persistence;

    [TestFixture]
    class EnvironmentControllerTests
    {
        [Test]
        public async Task Merges_the_data_of_every_provider()
        {
            var controller = new EnvironmentController(
            [
                new Provider(EnvironmentDatum.Value("Storage.Type", () => "RavenDB")),
                new Provider(EnvironmentDatum.Value("Host.ProcessorCount", () => "8"))
            ], NullLogger<EnvironmentController>.Instance);

            var result = await controller.Environment();

            var response = (EnvironmentController.EnvironmentDataResponse)result.Value;
            Assert.That(response.EnvironmentData, Is.EqualTo(new Dictionary<string, string>
            {
                ["Storage.Type"] = "RavenDB",
                ["Host.ProcessorCount"] = "8"
            }));
        }

        [Test]
        public async Task A_datum_that_cannot_be_read_costs_only_its_own_key()
        {
            var controller = new EnvironmentController(
            [
                new Provider(
                    EnvironmentDatum.Value("Storage.Type", () => "RavenDB"),
                    EnvironmentDatum.Deferred("Storage.ServerVersion", _ => throw new InvalidOperationException("server down")))
            ], NullLogger<EnvironmentController>.Instance);

            var result = await controller.Environment();

            var response = (EnvironmentController.EnvironmentDataResponse)result.Value;
            Assert.That(response.EnvironmentData, Is.EqualTo(new Dictionary<string, string>
            {
                ["Storage.Type"] = "RavenDB",
                ["Storage.ServerVersion"] = EnvironmentDatum.ReadFailed
            }));
        }

        [Test]
        public async Task A_provider_that_cannot_list_its_data_costs_only_its_own_keys()
        {
            var controller = new EnvironmentController(
            [
                new ThrowingProvider(),
                new Provider(EnvironmentDatum.Value("Host.ProcessorCount", () => "8"))
            ], NullLogger<EnvironmentController>.Instance);

            var result = await controller.Environment();

            var response = (EnvironmentController.EnvironmentDataResponse)result.Value;
            Assert.That(response.EnvironmentData, Is.EqualTo(new Dictionary<string, string>
            {
                ["Host.ProcessorCount"] = "8"
            }));
        }

        [Test]
        public void Cancellation_propagates()
        {
            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            var controller = new EnvironmentController(
            [
                new Provider(EnvironmentDatum.Deferred("Storage.ServerVersion", cancellationToken =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return new ValueTask<string>("unreached");
                }))
            ], NullLogger<EnvironmentController>.Instance);

            Assert.ThrowsAsync<OperationCanceledException>(() => controller.Environment(cancellationTokenSource.Token));
        }

        class Provider(params EnvironmentDatum[] data) : IEnvironmentDataProvider
        {
            public IEnumerable<EnvironmentDatum> GetData() => data;
        }

        class ThrowingProvider : IEnvironmentDataProvider
        {
            public IEnumerable<EnvironmentDatum> GetData() => throw new InvalidOperationException("cannot list");
        }
    }
}
