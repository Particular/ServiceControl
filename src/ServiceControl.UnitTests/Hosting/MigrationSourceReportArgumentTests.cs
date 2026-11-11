namespace ServiceControl.UnitTests.Hosting;

using NUnit.Framework;
using Particular.ServiceControl.Hosting;
using ServiceControl.Hosting.Commands;

[TestFixture]
class MigrationSourceReportArgumentTests
{
    [Test]
    public void The_flag_selects_the_report_command() =>
        Assert.That(new HostArguments(["--migration-source-report"]).Command, Is.EqualTo(typeof(MigrationSourceReportCommand)));

    [Test]
    public void The_migrate_flag_selects_the_offline_command() =>
        Assert.That(new HostArguments(["--migrate"]).Command, Is.EqualTo(typeof(MigrateCommand)));

    [Test]
    public void An_ingestion_worker_cannot_run_the_offline_command() =>
        Assert.That(new HostArguments(["--migrate", "--error-ingestion-only"]).Help, Is.True);

    [Test]
    public void No_flag_still_selects_the_run_command() =>
        Assert.That(new HostArguments([]).Command, Is.EqualTo(typeof(RunCommand)));

    [Test]
    public void The_setup_flag_is_undisturbed() =>
        Assert.That(new HostArguments(["--setup"]).Command, Is.EqualTo(typeof(SetupCommand)));
}
