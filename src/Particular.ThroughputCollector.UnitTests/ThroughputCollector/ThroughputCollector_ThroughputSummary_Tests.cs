namespace Particular.ThroughputCollector.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Particular.ThroughputCollector.Contracts;
    using Particular.ThroughputCollector.UnitTests.Infrastructure;

    [TestFixture]
    class ThroughputCollector_ThroughputSummary_Tests : ThroughputCollectorTestFixture
    {
        readonly Broker broker = Broker.AzureServiceBus;
        public override Task Setup()
        {
            SetThroughputSettings = s =>
            {
                s.Broker = broker;
            };

            return base.Setup();
        }


        [Test]
        public async Task Should_remove_audit_error_and_servicecontrol_queue_from_summary()
        {
            EndpointsWithThroughputFromBrokerOnly.ForEach(e => DataStore.RecordEndpointThroughput(e));
            await DataStore.RecordEndpointThroughput(ErrorEndpoint);
            await DataStore.RecordEndpointThroughput(AuditEndpoint);
            await DataStore.RecordEndpointThroughput(ServiceControlEndpoint);

            var summary = await ThroughputCollector.GetThroughputSummary();

            Assert.That(summary, Is.Not.Null);
            Assert.That(summary.Count, Is.EqualTo(3), $"Incorrect number of endpoints in throughput summary");

        }
        [Test]
        public async Task Should_return_correct_number_of_endpoints_in_summary_when_only_one_source_of_throughput()
        {
            EndpointsWithThroughputFromBrokerOnly.ForEach(e => DataStore.RecordEndpointThroughput(e));

            var summary = await ThroughputCollector.GetThroughputSummary();

            Assert.That(summary, Is.Not.Null);
            Assert.That(summary.Count, Is.EqualTo(3), $"Incorrect number of endpoints in throughput summary");
        }

        [Test]
        public async Task Should_return_correct_number_of_endpoints_in_summary_when_multiple_sources_of_throughput()
        {
            EndpointsWithThroughputFromBrokerAndMonitoring.ForEach(e => DataStore.RecordEndpointThroughput(e));

            var summary = await ThroughputCollector.GetThroughputSummary();

            Assert.That(summary, Is.Not.Null);
            Assert.That(summary.Count, Is.EqualTo(3), $"Incorrect number of endpoints in throughput summary");
        }

        [Test]
        public async Task Should_return_correct_max_throughput_in_summary_when_data_only_from_one_source()
        {
            EndpointsWithThroughputFromBrokerOnly.ForEach(e => DataStore.RecordEndpointThroughput(e));

            var summary = await ThroughputCollector.GetThroughputSummary();

            Assert.That(summary, Is.Not.Null);
            Assert.That(summary.Count, Is.EqualTo(3));

            Assert.That(summary.FirstOrDefault(w => w.Name == "Endpoint1").MaxDailyThroughput, Is.EqualTo(55), $"Incorrect MaxDailyThroughput recorded for Endpoint1");
            Assert.That(summary.FirstOrDefault(w => w.Name == "Endpoint2").MaxDailyThroughput, Is.EqualTo(65), $"Incorrect MaxDailyThroughput recorded for Endpoint2");
            Assert.That(summary.FirstOrDefault(w => w.Name == "Endpoint3").MaxDailyThroughput, Is.EqualTo(75), $"Incorrect MaxDailyThroughput recorded for Endpoint3");

        }

        [Test]
        public async Task Should_return_correct_max_throughput_in_summary_when_multiple_sources()
        {
            EndpointsWithThroughputFromBrokerAndMonitoringAndAudit.ForEach(e => DataStore.RecordEndpointThroughput(e));

            var summary = await ThroughputCollector.GetThroughputSummary();

            Assert.That(summary, Is.Not.Null);
            Assert.That(summary.Count, Is.EqualTo(3));

            Assert.That(summary.FirstOrDefault(w => w.Name == "Endpoint1").MaxDailyThroughput, Is.EqualTo(65), $"Incorrect MaxDailyThroughput recorded for Endpoint1");
            Assert.That(summary.FirstOrDefault(w => w.Name == "Endpoint2").MaxDailyThroughput, Is.EqualTo(65), $"Incorrect MaxDailyThroughput recorded for Endpoint2");
            Assert.That(summary.FirstOrDefault(w => w.Name == "Endpoint3").MaxDailyThroughput, Is.EqualTo(57), $"Incorrect MaxDailyThroughput recorded for Endpoint3");
        }

        [Test]
        public async Task Should_return_correct_max_throughput_in_summary_when_endpoint_has_no_throughput()
        {
            EndpointsWithNoThroughput.ForEach(e => DataStore.RecordEndpointThroughput(e));

            var summary = await ThroughputCollector.GetThroughputSummary();

            Assert.That(summary, Is.Not.Null);
            Assert.That(summary.Count, Is.EqualTo(1), "Invalid number of endpoints in throughput summary");
            Assert.That(summary[0].MaxDailyThroughput, Is.EqualTo(0), $"Incorrect MaxDailyThroughput recorded for {summary[0].Name}");
        }

        [Test]
        public async Task Should_return_correct_max_throughput_in_summary_when_data_from_multiple_sources_and_name_is_different()
        {
            EndpointsWithDifferentNamesButSameSanitizedNames.ForEach(e => DataStore.RecordEndpointThroughput(e));

            var summary = await ThroughputCollector.GetThroughputSummary();

            Assert.That(summary, Is.Not.Null);
            Assert.That(summary.Count, Is.EqualTo(1));

            //we want to see the name for the endpoint if one exists, not the broker sanitized name
            Assert.That(summary[0].Name, Is.EqualTo("Endpoint1_"), $"Incorrect Name for Endpoint1");

            //even though the names are different, we should have matched on the sanitized name and hence displayed max throughput from the 2 endpoints
            Assert.That(summary[0].MaxDailyThroughput, Is.EqualTo(75), $"Incorrect MaxDailyThroughput recorded for Endpoint1");
        }


        List<Endpoint> EndpointsWithNoThroughput =
        [
            new Endpoint { Name = "Endpoint1", SanitizedName = "Endpoint1", ThroughputSource = ThroughputSource.Audit },
        ];

        List<Endpoint> EndpointsWithThroughputFromBrokerOnly =
        [
            new Endpoint { Name = "Endpoint1", SanitizedName = "Endpoint1", ThroughputSource = ThroughputSource.Broker, DailyThroughput = [new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 50 }, new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 55 }] },
            new Endpoint { Name = "Endpoint2", SanitizedName = "Endpoint2", ThroughputSource = ThroughputSource.Broker, DailyThroughput = [new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 60 }, new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 65 }] },
            new Endpoint { Name = "Endpoint3", SanitizedName = "Endpoint3", ThroughputSource = ThroughputSource.Broker, DailyThroughput = [new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 75 }, new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 50 }] }
        ];

        List<Endpoint> EndpointsWithThroughputFromBrokerAndMonitoring =
        [
            new Endpoint { Name = "Endpoint1", SanitizedName = "Endpoint1", ThroughputSource = ThroughputSource.Broker, DailyThroughput = [new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 50 }, new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 55 }] },
            new Endpoint { Name = "Endpoint1", SanitizedName = "Endpoint1", ThroughputSource = ThroughputSource.Monitoring, DailyThroughput = [new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 60 }, new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 65 }] },
            new Endpoint { Name = "Endpoint2", SanitizedName = "Endpoint2", ThroughputSource = ThroughputSource.Broker, DailyThroughput = [new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 60 }, new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 65 }] },
            new Endpoint { Name = "Endpoint3", SanitizedName = "Endpoint3", ThroughputSource = ThroughputSource.Broker, DailyThroughput = [new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 50 }, new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 55 }] },
            new Endpoint { Name = "Endpoint3", SanitizedName = "Endpoint3", ThroughputSource = ThroughputSource.Monitoring, DailyThroughput = [new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 40 }, new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 45 }] }
        ];

        List<Endpoint> EndpointsWithThroughputFromBrokerAndMonitoringAndAudit =
        [
            new Endpoint { Name = "Endpoint1", SanitizedName = "Endpoint1", ThroughputSource = ThroughputSource.Broker, DailyThroughput = [new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 50 }, new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 55 }] },
            new Endpoint { Name = "Endpoint1", SanitizedName = "Endpoint1", ThroughputSource = ThroughputSource.Monitoring, DailyThroughput = [new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 60 }, new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 65 }] },
            new Endpoint { Name = "Endpoint2", SanitizedName = "Endpoint2", ThroughputSource = ThroughputSource.Broker, DailyThroughput = [new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 60 }, new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 65 }] },
            new Endpoint { Name = "Endpoint2", SanitizedName = "Endpoint2", ThroughputSource = ThroughputSource.Audit, DailyThroughput = [new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 61 }, new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 64 }] },
            new Endpoint { Name = "Endpoint3", SanitizedName = "Endpoint3", ThroughputSource = ThroughputSource.Broker, DailyThroughput = [new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 50 }, new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 57 }] },
            new Endpoint { Name = "Endpoint3", SanitizedName = "Endpoint3", ThroughputSource = ThroughputSource.Monitoring, DailyThroughput = [new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 40 }, new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 45 }] },
            new Endpoint { Name = "Endpoint3", SanitizedName = "Endpoint3", ThroughputSource = ThroughputSource.Audit, DailyThroughput = [new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 42 }, new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 47 }] }
        ];

        List<Endpoint> EndpointsWithDifferentNamesButSameSanitizedNames =
        [
            new Endpoint { Name = "Endpoint1", SanitizedName = "Endpoint1", ThroughputSource = ThroughputSource.Broker, DailyThroughput = [new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 50 }, new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 75 }] },
            new Endpoint { Name = "Endpoint1_", SanitizedName = "Endpoint1", ThroughputSource = ThroughputSource.Audit, DailyThroughput = [new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 60 }, new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 65 }] },
        ];

        Endpoint ServiceControlEndpoint = new Endpoint { Name = "Particular.ServiceControl", SanitizedName = "Particular.ServiceControl", ThroughputSource = ThroughputSource.Broker, DailyThroughput = [new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 500 }, new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 600 }] };
        Endpoint AuditEndpoint = new Endpoint { Name = "audit", SanitizedName = "audit", ThroughputSource = ThroughputSource.Broker, DailyThroughput = [new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 500 }, new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 600 }] };
        Endpoint ErrorEndpoint = new Endpoint { Name = "error", SanitizedName = "error", ThroughputSource = ThroughputSource.Broker, DailyThroughput = [new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 500 }, new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 600 }] };
    }
}