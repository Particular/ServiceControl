namespace ServiceControl.UnitTests.PlatformHealth
{
    using System;
    using System.Text.Json;
    using NUnit.Framework;
    using ServiceControl.Api.Contracts;
    using ServiceControl.Contracts.CustomChecks;
    using ServiceControl.Infrastructure.WebApi;
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

        [Test]
        public void Delayed_failure_does_not_replace_a_newer_recovery()
        {
            var state = new PlatformHealthState();
            var recovery = Detail("ServiceControl Primary Instance", hasFailed: false);
            recovery.ReportedAt = recovery.ReportedAt.AddMinutes(1);

            state.Record(recovery);
            state.Record(Detail("ServiceControl Primary Instance", hasFailed: true));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(state.GetHealth().Status, Is.EqualTo("healthy"));
                Assert.That(state.GetChecks()[0].ReportedAt, Is.EqualTo(recovery.ReportedAt));
            }
        }

        [Test]
        public void Snapshots_include_passes_and_do_not_change_with_later_reports()
        {
            var state = new PlatformHealthState();
            var detail = Detail("ServiceControl Primary Instance", hasFailed: false);
            state.Record(detail);
            var snapshot = state.GetChecks();

            detail.HasFailed = true;
            detail.FailureReason = "Later failure";
            detail.ReportedAt = detail.ReportedAt.AddMinutes(1);
            state.Record(detail);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(snapshot, Has.Length.EqualTo(1));
                Assert.That(snapshot[0].HasFailed, Is.False);
                Assert.That(snapshot[0].Message, Is.Null);
                Assert.That(state.GetChecks()[0].HasFailed, Is.True);
                Assert.That(state.GetChecks()[0].Id, Is.EqualTo(snapshot[0].Id));
            }
        }

        [Test]
        public void Same_named_checks_on_different_hosts_have_distinct_stably_ordered_ids()
        {
            var state = new PlatformHealthState();
            var first = Detail("Audit Message Ingestion", hasFailed: true);
            var second = Detail("Audit Message Ingestion", hasFailed: false);
            second.OriginatingEndpoint.HostId = Guid.Parse("627A66F4-F7C5-4D18-8793-0D8C385A5744");

            state.Record(second);
            state.Record(first);
            var snapshot = state.GetChecks();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(snapshot, Has.Length.EqualTo(2));
                Assert.That(snapshot[0].Id, Is.Not.EqualTo(snapshot[1].Id));
                Assert.That(snapshot, Is.Ordered.By(nameof(PlatformHealthState.CheckState.Id)));
                Assert.That(state.GetHealth().Alerts, Has.Length.EqualTo(1));
            }
        }

        [Test]
        public void Expanded_contract_preserves_wire_names_and_false_values_but_omits_unknown_fields()
        {
            var health = new PlatformHealthView
            {
                Status = "unknown",
                Severity = "unknown",
                Alerts = [],
                Instances = [new PlatformHealthInstance
                {
                    Id = "primary",
                    Name = "ServiceControl",
                    ApiUrl = "https://localhost/servicecontrol/api/",
                    Kind = "error",
                    Role = "primary-error",
                    Version = "6.10.0",
                    Health = "healthy",
                    ObservedAt = new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero),
                    ForwardErrorMessages = false,
                    ErrorRetentionPeriod = TimeSpan.FromDays(14)
                }]
            };

            using var json = JsonDocument.Parse(JsonSerializer.Serialize(health, SerializerOptions.Default));
            var instance = json.RootElement.GetProperty("instances")[0];

            using (Assert.EnterMultipleScope())
            {
                Assert.That(json.RootElement.GetProperty("status").GetString(), Is.EqualTo("unknown"));
                Assert.That(json.RootElement.GetProperty("alerts").GetArrayLength(), Is.Zero);
                Assert.That(instance.GetProperty("api_url").GetString(), Is.EqualTo("https://localhost/servicecontrol/api/"));
                Assert.That(instance.GetProperty("version").GetString(), Is.EqualTo("6.10.0"));
                Assert.That(instance.GetProperty("forward_error_messages").GetBoolean(), Is.False);
                Assert.That(instance.GetProperty("error_retention_period").GetString(), Is.EqualTo("14.00:00:00"));
                Assert.That(instance.GetProperty("observed_at").GetDateTimeOffset(), Is.EqualTo(health.Instances[0].ObservedAt));
                Assert.That(instance.GetProperty("health_signals_status").GetString(), Is.EqualTo("unreported"));
                Assert.That(instance.TryGetProperty("audit_retention_period", out _), Is.False);
                Assert.That(instance.TryGetProperty("last_reported_at", out _), Is.False);
                Assert.That(json.RootElement.TryGetProperty("license", out _), Is.False);
            }
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