namespace ServiceControl.Audit.UnitTests.Infrastructure
{
    using Audit.Infrastructure.Hosting;
    using Audit.Infrastructure.Hosting.Commands;
    using NUnit.Framework;

    [TestFixture]
    class HostArgumentsTests
    {
        [Test]
        public void No_arguments_runs_the_instance() =>
            Assert.That(new HostArguments([]).Command, Is.EqualTo(typeof(RunCommand)));

        [Test]
        public void Apply_retention_selects_the_apply_retention_command() =>
            Assert.That(new HostArguments(["--apply-retention"]).Command, Is.EqualTo(typeof(ApplyRetentionCommand)));

        [Test]
        public void Import_failed_audits_selects_the_import_command() =>
            Assert.That(new HostArguments(["--import-failed-audits"]).Command, Is.EqualTo(typeof(ImportFailedAuditsCommand)));
    }
}
