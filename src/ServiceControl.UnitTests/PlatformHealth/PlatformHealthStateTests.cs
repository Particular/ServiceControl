namespace ServiceControl.UnitTests.PlatformHealth
{
    using System;
    using NUnit.Framework;
    using ServiceControl.Contracts.CustomChecks;
    using ServiceControl.Operations;
    using ServiceControl.PlatformHealth;

    [TestFixture]
    class PlatformHealthStateTests
    {
        [Test]
        public void Health_is_unknown_until_an_internal_check_reports()
        {
            var health = new PlatformHealthState().GetHealth();

            Assert.That(health.Status, Is.EqualTo("unknown"));
            Assert.That(health.Severity, Is.EqualTo("unknown"));
            Assert.That(health.Alerts, Is.Empty);
        }

        [Test]
        public void Failed_internal_check_is_returned_as_an_alert_and_cleared_on_recovery()
        {
            var state = new PlatformHealthState();
            var detail = Detail("ServiceControl Primary Instance", hasFailed: true);

            state.Record(detail);

            var failingHealth = state.GetHealth();
            Assert.That(failingHealth.Status, Is.EqualTo("unhealthy"));
            Assert.That(failingHealth.Severity, Is.EqualTo("error"));
            Assert.That(failingHealth.Alerts, Has.Length.EqualTo(1));
            Assert.That(failingHealth.Alerts[0].CheckId, Is.EqualTo(detail.CustomCheckId));
            Assert.That(failingHealth.Alerts[0].Message, Is.EqualTo(detail.FailureReason));
            Assert.That(failingHealth.Alerts[0].InstanceName, Is.EqualTo(detail.OriginatingEndpoint.Name));
            Assert.That(failingHealth.Alerts[0].HostId, Is.EqualTo(detail.OriginatingEndpoint.HostId));

            detail.HasFailed = false;
            detail.FailureReason = null;
            state.Record(detail);

            var recoveredHealth = state.GetHealth();
            Assert.That(recoveredHealth.Status, Is.EqualTo("healthy"));
            Assert.That(recoveredHealth.Severity, Is.EqualTo("none"));
            Assert.That(recoveredHealth.Alerts, Is.Empty);
        }

        [Test]
        public void Customer_custom_checks_do_not_affect_platform_health()
        {
            var state = new PlatformHealthState();
            state.Record(Detail("Customer check", hasFailed: true));

            var health = state.GetHealth();

            Assert.That(health.Status, Is.EqualTo("unknown"));
            Assert.That(health.Alerts, Is.Empty);
        }

        [Test]
        public void Audit_internal_checks_are_included_with_their_originating_instance()
        {
            var state = new PlatformHealthState();
            var detail = Detail("Audit Message Ingestion", hasFailed: true);
            detail.OriginatingEndpoint.Name = "ServiceControl.Audit";

            state.Record(detail);

            var health = state.GetHealth();
            Assert.That(health.Status, Is.EqualTo("unhealthy"));
            Assert.That(health.Alerts, Has.Length.EqualTo(1));
            Assert.That(health.Alerts[0].InstanceName, Is.EqualTo("ServiceControl.Audit"));
        }

        static CustomCheckDetail Detail(string checkId, bool hasFailed) => new()
        {
            CustomCheckId = checkId,
            Category = "ServiceControl Health",
            HasFailed = hasFailed,
            FailureReason = hasFailed ? "Check failed" : null,
            ReportedAt = new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc),
            OriginatingEndpoint = new EndpointDetails
            {
                Name = "ServiceControl",
                Host = "localhost",
                HostId = Guid.Parse("82E379F4-A5BD-4D83-8B64-70488BC6ED3A")
            }
        };
    }
}