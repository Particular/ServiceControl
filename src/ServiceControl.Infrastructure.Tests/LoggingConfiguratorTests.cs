#nullable enable
namespace ServiceControl.Infrastructure.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Config;
using NLog.Extensions.Logging;
using NLog.Targets;
using NUnit.Framework;
using ServiceControl.Infrastructure;
using ServiceControl.Infrastructure.Auth;
using LogLevel = NLog.LogLevel;

[TestFixture]
public class LoggingConfiguratorTests
{
    static LoggingConfiguration BuildConfig() =>
        LoggingConfigurator.BuildConfiguration("logfile.txt", Path.GetTempPath(), LogLevel.Info);

    static List<LoggingRule> AuditRules(LoggingConfiguration config) =>
        [.. config.LoggingRules.Where(r => r.Targets.Any(t => t.Name == "audit-console"))];

    static NLog.Layouts.Layout LayoutOf(LoggingConfiguration config, string targetName) =>
        config.AllTargets.OfType<TargetWithLayout>().Single(t => t.Name == targetName).Layout;

    [Test]
    public void Audit_target_emits_the_prerendered_event_verbatim()
    {
        var rendered = LayoutOf(BuildConfig(), "audit-console").Render(new LogEventInfo(LogLevel.Info, AuthorizationAuditLog.AuditCategory, "ECS-PAYLOAD"));

        Assert.That(rendered, Is.EqualTo("ECS-PAYLOAD"),
            "the audit target must pass the pre-rendered ECS JSON through unwrapped, not double-encode it");
    }

    [TestCase(AuthorizationAuditLog.AuditCategory)]
    [TestCase(MessageActionAuditLog.MessageCategory)]
    public void Audit_events_do_not_fall_through_to_the_operational_log(string category)
    {
        var config = BuildConfig();

        var auditRule = AuditRules(config).Single(r => r.NameMatches(category));
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
        captureConfig.AddRule(LogLevel.Info, LogLevel.Fatal, captured, AuthorizationAuditLog.AuditCategory);
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
            Assert.That(allow.GetProperty("@timestamp").GetString(), Is.Not.Empty, "ECS @timestamp should be present");
            Assert.That(allow.GetProperty("event").GetProperty("kind").GetString(), Is.EqualTo("event"));
            Assert.That(allow.GetProperty("event").GetProperty("category")[0].GetString(), Is.EqualTo("iam"));
            Assert.That(allow.GetProperty("event").GetProperty("type")[0].GetString(), Is.EqualTo("allowed"));
            Assert.That(allow.GetProperty("event").GetProperty("action").GetString(), Is.EqualTo("error:messages:retry"));
            Assert.That(allow.GetProperty("event").GetProperty("outcome").GetString(), Is.EqualTo("success"));
            Assert.That(allow.GetProperty("user").GetProperty("id").GetString(), Is.EqualTo("alice-sub-001"));
            Assert.That(allow.GetProperty("user").GetProperty("name").GetString(), Is.EqualTo("Alice Smith"));
            Assert.That(allow.GetProperty("servicecontrol").GetProperty("resource").GetString(), Is.EqualTo("acme.sales"));
        });

        var deny = JsonDocument.Parse(captured.Logs[1]).RootElement;
        Assert.Multiple(() =>
        {
            Assert.That(deny.GetProperty("event").GetProperty("type")[0].GetString(), Is.EqualTo("denied"));
            Assert.That(deny.GetProperty("event").GetProperty("outcome").GetString(), Is.EqualTo("failure"));
            Assert.That(deny.GetProperty("user").GetProperty("id").GetString(), Is.EqualTo("bob-sub-002"));
            Assert.That(deny.GetProperty("servicecontrol").TryGetProperty("resource", out _), Is.False, "absent resource should be omitted from the JSON entirely, not present as null");
        });
    }

    [Test]
    public void Message_action_subcategory_is_captured_by_the_audit_rule()
    {
        var auditRules = AuditRules(BuildConfig());

        Assert.That(auditRules.Any(r => r.NameMatches(MessageActionAuditLog.MessageCategory)), Is.True);
        Assert.That(auditRules.Any(r => r.NameMatches(MessageActionAuditLog.OperationCategory)), Is.True);
    }

    [Test]
    public void Audit_instance_loggers_write_to_the_operational_log_with_the_full_layout()
    {
        var config = BuildConfig();
        var operational = new MemoryTarget("operational-capture") { Layout = LayoutOf(config, "console") };
        var audit = new MemoryTarget("audit-capture") { Layout = LayoutOf(config, "audit-console") };
        using var logFactory = new LogFactory { Configuration = RouteToMemory(config, operational, audit) };

        logFactory.GetLogger("ServiceControl.Audit.Auditing.AuditPersister").Error(new InvalidOperationException("boom"), "Processing of message failed");
        logFactory.Flush();

        Assert.Multiple(() =>
        {
            Assert.That(operational.Logs, Has.One.EndsWith("|Error|ServiceControl.Audit.Auditing.AuditPersister|Processing of message failed|System.InvalidOperationException: boom"),
                "an audit instance logger must keep the level, logger name and exception of the operational layout");
            Assert.That(audit.Logs, Is.Empty, "only the authorization and message action categories belong in the audit trail");
        });
    }

    static LoggingConfiguration RouteToMemory(LoggingConfiguration config, Target operational, Target audit)
    {
        var routed = new LoggingConfiguration();

        foreach (var rule in config.LoggingRules)
        {
            var targets = rule.Targets
                .Select(t => t.Name switch
                {
                    "console" => operational,
                    "audit-console" => audit,
                    _ => null
                })
                .OfType<Target>()
                .ToList();

            rule.Targets.Clear();
            targets.ForEach(rule.Targets.Add);
            routed.LoggingRules.Add(rule);
        }

        return routed;
    }
}
