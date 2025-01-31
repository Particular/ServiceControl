#nullable enable

namespace ServiceControl.RavenDB;

using System.Reflection;
using System.Security.Cryptography.X509Certificates;

public static class RavenClientCertificate
{
    public static X509Certificate2? FindClientCertificate()
    {
        var applicationDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? string.Empty;
        var certificatePath = Path.Combine(applicationDirectory, "raven-client-certificate.pfx");

        if (File.Exists(certificatePath))
        {
            return new X509Certificate2(certificatePath);
        }
        return null;
    }
}