
using System.IO;
using NUnit.Framework;
using ServiceControl.Infrastructure.RavenDB;

[TestFixture]
class RavenBootstrapperTests
{
    [Test]
    public void CanReadLicense()
    {
        var readLicense = ReadLicense();
        Assert.IsNotNullOrEmpty(readLicense);
    }

    static string ReadLicense()
    {
        using (var resourceStream = typeof(RavenBootstrapper).Assembly.GetManifestResourceStream("ServiceControl.Infrastructure.RavenDB.RavenLicense.xml"))
        using (var reader = new StreamReader(resourceStream))
        {
            return reader.ReadToEnd();
        }
    }
}