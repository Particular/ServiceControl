#nullable enable
namespace ServiceControl.Infrastructure.Tests;

using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Config;
using NLog.Extensions.Logging;
using NLog.Layouts;
using NLog.Targets;
using NUnit.Framework;
using ServiceControl.Infrastructure;
using ServiceControl.Infrastructure.Auth;
using LogLevel = NLog.LogLevel;

[TestFixture]
public class LoggingConfiguratorTests
{
    static readonly string AuditPattern = $"{AuthorizationAuditLog.AuditCategory}*";

    static LoggingConfiguration BuildConfig() =>
        LoggingConfigurator.BuildConfiguration("logfile.txt", Path.GetTempPath(), LogLevel.Info);

    [Test]
    public void Audit_category_is_routed_to_a_structured_json_target()
    {
        var config = BuildConfig();

        var auditRule = config.LoggingRules.Single(r => r.LoggerNamePattern == AuditPattern);

        Assert.That(
            auditRule.Targets.OfType<TargetWithLayout>().Any(t => t.Layout is JsonLayout),
            Is.True,
            "the audit category should be routed to a target that uses a JSON layout");
    }

    [Test]
    public void Audit_events_do_not_fall_through_to_the_operational_log()
    {
        var config = BuildConfig();

        var auditRule = config.LoggingRules.Single(r => r.LoggerNamePattern == AuditPattern);
        var operationalConsoleRule = config.LoggingRules.Single(r => r.LoggerNamePattern == "*" && r.Targets.Any(t => t.Name == "console"));

        Assert.That(auditRule.Final, Is.True, "the audit rule must be final so audit JSON is not duplicated into the plain-text operational log");
        Assert.That(
            config.LoggingRules.IndexOf(auditRule),
            Is.LessThan(config.LoggingRules.IndexOf(operationalConsoleRule)),
            "the audit rule must be evaluated before the catch-all console rule for Final to take effect");
    }

    [Test]
    public void Audit_decisions_render_as_valid_structured_json()
    {
        // Use the exact JSON layout the production configuration builds...
        var auditLayout = BuildConfig().AllTargets
            .OfType<TargetWithLayout>()
            .Single(t => t.Name == "audit-console")
            .Layout;

        // ...and capture what it renders, driven through the real audit logger over an isolated NLog factory.
        var captured = new MemoryTarget("audit-capture") { Layout = auditLayout };
        var captureConfig = new LoggingConfiguration();
        captureConfig.AddRule(LogLevel.Info, LogLevel.Fatal, captured, AuditPattern);
        var logFactory = new LogFactory { Configuration = captureConfig };

        using (var loggerFactory = LoggerFactory.Create(b => b.AddNLog(_ => logFactory)))
        {
            var audit = new AuthorizationAuditLog(loggerFactory);
            audit.Decision("alice-sub-001", "Alice Smith", "error:messages:retry", "acme.sales", allowed: true, reason: "role:sc-operator matched");
            audit.Decision("bob-sub-002", "Bob Jones", "error:messages:retry", null, allowed: false, reason: "no matching role");
        }

        logFactory.Flush();

        Assert.That(captured.Logs, Has.Count.EqualTo(2), "expected one JSON line per decision");

        foreach (var line in captured.Logs)
        {
            TestContext.Progress.WriteLine(line);
        }

        var allow = JsonDocument.Parse(captured.Logs[0]).RootElement;
        Assert.Multiple(() =>
        {
            Assert.That(allow.GetProperty("level").GetString(), Is.EqualTo("INFO"));
            Assert.That(allow.GetProperty("category").GetString(), Is.EqualTo(AuthorizationAuditLog.AuditCategory));
            Assert.That(allow.GetProperty("SubjectId").GetString(), Is.EqualTo("alice-sub-001"));
            Assert.That(allow.GetProperty("SubjectName").GetString(), Is.EqualTo("Alice Smith"));
            Assert.That(allow.GetProperty("Permission").GetString(), Is.EqualTo("error:messages:retry"));
            Assert.That(allow.GetProperty("Resource").GetString(), Is.EqualTo("acme.sales"));
            Assert.That(allow.TryGetProperty("timestamp", out _), Is.True, "timestamp attribute should be present");
        });

        var deny = JsonDocument.Parse(captured.Logs[1]).RootElement;
        Assert.Multiple(() =>
        {
            Assert.That(deny.GetProperty("level").GetString(), Is.EqualTo("WARN"), "denies must surface at Warning level");
            Assert.That(deny.GetProperty("SubjectId").GetString(), Is.EqualTo("bob-sub-002"));
            Assert.That(deny.GetProperty("Permission").GetString(), Is.EqualTo("error:messages:retry"));
        });
    }
}
