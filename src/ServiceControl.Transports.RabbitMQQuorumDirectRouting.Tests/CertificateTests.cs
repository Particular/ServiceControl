namespace ServiceControl.Transport.Tests;

using NUnit.Framework;
using ServiceControl.Transports;
using System.IO;

[TestFixture]
class CertificateTests
{
    [Test]
    public void Passing_CertPath_In_ConnectionString_Sets_The_ClientCertificate_Correctly()
    {
        var certificateLocation = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "cert.cer");

        var connectionString = $"host=localhost;user=guest;pass=guest;certPath='{certificateLocation}'";
        var transportSettings = new TransportSettings { ConnectionString = connectionString };

        var customizer = new ExtendedClassToMakeProtectedMethodsPublic();
        var transport = customizer.PublicCreateTrasport(transportSettings);

        Assert.That(transport.ClientCertificate, Is.Not.Null);
    }

    [Test]
    public void Passing_certPassphrase_In_ConnectionString_Sets_The_ClientCertificate_Correctly()
    {
        var certificateLocation = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "passwordcert.pfx");

        var connectionString = $"host=localhost;user=guest;pass=guest;certPath='{certificateLocation}';certPassphrase='MyPassword';";
        var transportSettings = new TransportSettings { ConnectionString = connectionString };

        var customizer = new ExtendedClassToMakeProtectedMethodsPublic();
        var transport = customizer.PublicCreateTrasport(transportSettings);

        Assert.That(transport.ClientCertificate, Is.Not.Null);
    }
}