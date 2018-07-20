using NUnit.Framework;
using ServiceControl.Infrastructure.RavenDB;

[TestFixture]
class RavenBootstrapperTests
{
    [Test]
    public void ReadLicense()
    {
        var readLicense = RavenBootstrapper.ReadLicense();
        Assert.IsNotNull(readLicense);
        Assert.IsNotEmpty(readLicense);
    }
}