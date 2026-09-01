namespace ServiceControl.Config.Tests
{
    using NUnit.Framework;
    using UI.Shell;

    [TestFixture]
    class SetupModeTests
    {
        [TestCase(SetupMode.ErrorHandling, true, false, false)]
        [TestCase(SetupMode.ErrorAndAudit, true, true, false)]
        [TestCase(SetupMode.AuditOnly, false, true, false)]
        [TestCase(SetupMode.MonitoringOnly, false, false, true)]
        public void Scenario_selects_the_instances_to_install(SetupMode mode, bool serviceControl, bool audit, bool monitoring)
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(mode.InstallsServiceControl(), Is.EqualTo(serviceControl));
                Assert.That(mode.InstallsAudit(), Is.EqualTo(audit));
                Assert.That(mode.InstallsMonitoring(), Is.EqualTo(monitoring));
            }
        }

        [TestCase(SetupMode.ErrorHandling)]
        [TestCase(SetupMode.ErrorAndAudit)]
        [TestCase(SetupMode.AuditOnly)]
        [TestCase(SetupMode.MonitoringOnly)]
        public void Every_scenario_installs_at_least_one_instance(SetupMode mode)
        {
            // The Next button is always enabled, so no scenario may resolve to nothing.
            Assert.That(mode.InstallsServiceControl() || mode.InstallsAudit() || mode.InstallsMonitoring(), Is.True);
        }

        [TestCase(SetupMode.ErrorHandling)]
        [TestCase(SetupMode.ErrorAndAudit)]
        public void Integrated_ServicePulse_follows_the_choice_when_an_error_instance_is_installed(SetupMode mode)
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(mode.InstallsServicePulse(wanted: true), Is.True);
                Assert.That(mode.InstallsServicePulse(wanted: false), Is.False);
            }
        }

        [TestCase(SetupMode.AuditOnly)]
        [TestCase(SetupMode.MonitoringOnly)]
        public void Integrated_ServicePulse_is_never_installed_without_an_error_instance(SetupMode mode)
        {
            Assert.That(mode.InstallsServicePulse(wanted: true), Is.False);
        }
    }
}
