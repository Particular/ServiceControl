namespace ServiceControl.UnitTests.CustomChecks
{
    using System;
    using System.Text.Json;
    using NUnit.Framework;
    using ServiceControl.Contracts.CustomChecks;
    using ServiceControl.Infrastructure.WebApi;
    using ServiceControl.Operations;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.Infrastructure;

    [TestFixture]
    class InternalCustomCheckClassificationTests
    {
        static CustomCheckView Check(string id, string category = "Health") => new()
        {
            Id = "CustomChecks/1",
            CustomCheckId = id,
            Category = category,
            Status = Status.Fail,
            ReportedAt = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc),
            OriginatingEndpoint = new EndpointDetails { Name = "test-host", Host = "localhost", HostId = DeterministicGuid.MakeId("test-host", "host") }
        };

        [Test]
        public void Internal_checks_are_flagged_internal()
        {
            var check = Check("ServiceControl Primary Instance");

            Assert.That(check.Internal, Is.True);
        }

        [TestCase("Error Message Ingestion")]
        [TestCase("Dead Letter Queue")]
        [TestCase("ServiceControl body storage")]
        [TestCase("Audit Message Ingestion Process")]
        public void Every_shipped_check_is_internal(string id)
        {
            var check = Check(id);

            Assert.That(check.Internal, Is.True, $"{id} is not in the registry");
        }

        [Test]
        public void Matching_ignores_case_and_category_so_the_same_id_works_for_primary_and_audit()
        {
            // "RavenDB dirty memory" is reported by the primary under "ServiceControl Health"
            // and by the audit instance under "ServiceControl.Audit Health"
            var primary = Check("RavenDB dirty memory", "ServiceControl Health");
            var audit = Check("ravendb dirty memory", "ServiceControl.Audit Health");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(primary.Internal, Is.True);
                Assert.That(audit.Internal, Is.True);
            }
        }

        [Test]
        public void Endpoint_checks_are_not_internal()
        {
            var check = Check("MyCustomCheckId", "MyCategory");

            Assert.That(check.Internal, Is.False);
        }

        [Test]
        public void The_wire_shape_is_additive_only()
        {
            var check = Check("ServiceControl Primary Instance");

            var json = JsonSerializer.Serialize(new[] { check }, SerializerOptions.Default);

            // New field present:
            Assert.That(json, Does.Contain("\"internal\":true"));
            // Every pre-existing field still present, unchanged:
            Assert.That(json, Does.Contain("\"custom_check_id\":\"ServiceControl Primary Instance\""));
            Assert.That(json, Does.Contain("\"category\":\"Health\""));
            Assert.That(json, Does.Contain("\"status\":\"fail\""));
            Assert.That(json, Does.Contain("\"reported_at\""));
            Assert.That(json, Does.Contain("\"originating_endpoint\""));
        }

        [Test]
        public void External_checks_render_internal_false_on_the_wire()
        {
            var check = Check("MyCustomCheckId", "MyCategory");

            var json = JsonSerializer.Serialize(new[] { check }, SerializerOptions.Default);

            Assert.That(json, Does.Contain("\"internal\":false"));
        }
    }
}