namespace ServiceControl.Monitoring.UnitTests.API
{
    using NUnit.Framework;
    using Particular.Approvals;

    class SettingsTests
    {
        [Test]
        public void PlatformSampleSettings()
        {
            Approver.Verify(new Settings());
        }
    }
}