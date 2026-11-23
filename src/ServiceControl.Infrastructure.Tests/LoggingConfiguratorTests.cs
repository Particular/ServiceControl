#nullable enable
namespace ServiceControl.Infrastructure.Tests;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Config;
using NLog.Extensions.Logging;
using NLog.Targets;
using NUnit.Framework;
using ServiceControl.Configuration;
using ServiceControl.Infrastructure;
using ServiceControl.Infrastructure.Auth;
using LogLevel = NLog.LogLevel;

[TestFixture]
public class LoggingConfiguratorTests
{
    static readonly string AuditPattern = $"{AuthorizationAuditLog.AuditCategory}*";

    static LoggingConfiguration BuildConfig() =>
        LoggingConfigurator.BuildConfiguration("logfile.txt", Path.GetTempPath(), LogLevel.Info);

    static void IgnoreInAContainer()
    {
        if (AppEnvironment.RunningInContainer)
        {
            Assert.Ignore("In a container the configuration logs to the console and has no file target.");
        }
    }

    [Test]
    public void A_migration_run_logs_only_to_its_own_unarchived_file()
    {
        IgnoreInAContainer();

        var config = LoggingConfigurator.BuildConfiguration(LoggingConfigurator.MigrationLogFileName(new DateTime(2026, 11, 19, 10, 10, 3)), Path.GetTempPath(), LogLevel.Info, logToConsole: false);
        var file = config.FindTargetByName<FileTarget>("file")!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(config.LoggingRules.Where(rule => rule.LoggerNamePattern == "*").SelectMany(rule => rule.Targets).Select(target => target.Name), Is.EqualTo(new[] { "file" }));
            Assert.That(file.FileName.Render(LogEventInfo.CreateNullEvent()), Does.EndWith("migration-2026-11-19-101003.txt"));
            Assert.That(file.ArchiveEvery, Is.EqualTo(FileArchivePeriod.None));
            Assert.That(file.ArchiveAboveSize, Is.Zero);
            Assert.That(file.MaxArchiveFiles, Is.Zero);
        }
    }

    [Test]
    public void A_container_run_touches_no_folder_and_a_second_run_in_the_same_second_keeps_the_first_run_log()
    {
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var start = new DateTime(2026, 11, 19, 10, 10, 3);

        try
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(LoggingConfigurator.CreateMigrationLogFile(folder, start, runningInContainer: true), Is.EqualTo("migration-2026-11-19-101003.txt"));
                Assert.That(Directory.Exists(folder), Is.False, "a container run logs to the console, so it must not create the log folder");
            }

            var first = LoggingConfigurator.CreateMigrationLogFile(folder, start, runningInContainer: false);
            File.WriteAllText(Path.Combine(folder, first), "first run");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(LoggingConfigurator.CreateMigrationLogFile(folder, start, runningInContainer: false), Is.EqualTo("migration-2026-11-19-101003.txt"));
                Assert.That(File.ReadAllText(Path.Combine(folder, first)), Is.EqualTo("first run"));
            }
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Test]
    public void A_log_folder_that_cannot_be_written_stops_the_run_naming_the_folder_and_the_setting()
    {
        var notAFolder = Path.GetTempFileName();

        try
        {
            var exception = Assert.Throws<Exception>(() => LoggingConfigurator.CreateMigrationLogFile(Path.Combine(notAFolder, "logs"), DateTime.UtcNow, runningInContainer: false))!;

            Assert.That(exception.Message, Does.Contain(notAFolder).And.Contain("ServiceControl/LogPath"));
        }
        finally
        {
            File.Delete(notAFolder);
        }
    }

    [Test]
    public void A_normal_log_file_keeps_its_archive_rules()
    {
        IgnoreInAContainer();

        var target = BuildConfig().FindTargetByName<FileTarget>("file")!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(target.ArchiveEvery, Is.EqualTo(FileArchivePeriod.Day));
            Assert.That(target.ArchiveAboveSize, Is.Not.Zero);
            Assert.That(target.MaxArchiveFiles, Is.Not.Zero);
        }
    }

    [Test]
    public void Audit_target_emits_the_prerendered_event_verbatim()
    {
        var auditTarget = BuildConfig().LoggingRules
            .Single(r => r.LoggerNamePattern == AuditPattern)
            .Targets.OfType<TargetWithLayout>()
            .Single(t => t.Name == "audit-console");

        var rendered = auditTarget.Layout.Render(new LogEventInfo(LogLevel.Info, AuthorizationAuditLog.AuditCategory, "ECS-PAYLOAD"));

        Assert.That(rendered, Is.EqualTo("ECS-PAYLOAD"),
            "the audit target must pass the pre-rendered ECS JSON through unwrapped, not double-encode it");
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
        var config = BuildConfig();
        var auditRule = config.LoggingRules.Single(r => r.LoggerNamePattern == AuditPattern);

        Assert.That(auditRule.NameMatches(MessageActionAuditLog.MessageCategory), Is.True);
        Assert.That(auditRule.NameMatches(MessageActionAuditLog.OperationCategory), Is.True);
    }
}
