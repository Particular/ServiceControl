namespace ServiceControl.Monitoring.UnitTests.API
{
    using NUnit.Framework;
    using Particular.Approvals;

    class SettingsTests
    {
        [Test]
        public void PlatformSampleSettings()
        {
            var settings = new Settings();
            settings.LicenseFileText = null;

            Approver.Verify(settings);
        }
    }
}