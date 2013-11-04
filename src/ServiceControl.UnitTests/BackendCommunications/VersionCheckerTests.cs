namespace ServiceControl.UnitTests.BackendCommunications
{
    using System;
    using EndpointPlugin.Operations.ServiceControlBackend;
    using NUnit.Framework;

    public class VersionCheckerTests
    {
        [Test]
        public void CoreVersionCheck()
        {
            VersionChecker.CoreFileVersion = new Version(4, 1, 0);

            Assert.True(VersionChecker.CoreVersionIsAtLeast(4, 1));
            Assert.True(VersionChecker.CoreVersionIsAtLeast(4, 0));
            Assert.False(VersionChecker.CoreVersionIsAtLeast(5, 0));
            Assert.False(VersionChecker.CoreVersionIsAtLeast(4, 2));
            Assert.True(VersionChecker.CoreVersionIsAtLeast(3, 0));
        }

    }
}