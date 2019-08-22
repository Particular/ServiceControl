namespace ServiceControl.Monitoring.UnitTests.API
{
    using System.Configuration;
    using NUnit.Framework;
    using Particular.Approvals;

    class SettingsTests
    {
        [Test]
        public void PlatformSampleSettings()
        {
            Approver.Verify(Settings.Load(new SettingsReader(ConfigurationManager.AppSettings)));
        }
    }
}
