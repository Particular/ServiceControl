namespace ServiceControlInstaller.Engine.UnitTests.Ports
{
    using System;
    using NUnit.Framework;
    using ServiceControlInstaller.Engine.Ports;

    [TestFixture]
    public class PortTest
    {
        [Test, Explicit]
        public void TestIsPortAvailable()
        {
            Assert.DoesNotThrow( () => PortUtils.CheckAvailable(10000), "Port 10000 wasn't available"); // A Random Port
            Assert.Throws<Exception>( () => PortUtils.CheckAvailable(9090), "Port 9090 is available");      // ServicePulse
        }
    }
}
