#nullable enable

namespace ServiceControl.RavenDB;

using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

public static class RavenClientCertificate
{
    public static X509Certificate2? FindClientCertificate(string? base64String)
    {
        if (base64String is not null)
        {
            try
            {
                var bytes = Convert.FromBase64String(base64String);
                return new X509Certificate2(bytes);
            }
            catch (Exception x) when (x is FormatException or CryptographicException)
            {
                throw new Exception("Could not read the RavenDB client certificate from the configured Base64 value.", x);
            }
        }

        var applicationDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? string.Empty;
        var certificatePath = Path.Combine(applicationDirectory, "raven-client-certificate.pfx");

        if (File.Exists(certificatePath))
        {
            return new X509Certificate2(certificatePath);
        }
        return null;
    }
}