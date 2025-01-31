#nullable enable

namespace ServiceControl.RavenDB;

using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

public static class RavenClientCertificate
{
    public static X509Certificate2? FindClientCertificate(IRavenClientCertificateInfo certInfo)
    {
        if (certInfo.ClientCertificateBase64 is not null)
        {
            try
            {
                var bytes = Convert.FromBase64String(certInfo.ClientCertificateBase64);
                return new X509Certificate2(bytes, certInfo.ClientCertificatePassword);
            }
            catch (Exception x) when (x is FormatException or CryptographicException)
            {
                throw new Exception("Could not read the RavenDB client certificate from the configured Base64 value.", x);
            }
        }

        if (certInfo.ClientCertificatePath is not null)
        {
            return new X509Certificate2(certInfo.ClientCertificatePath, certInfo.ClientCertificatePassword);
        }

        var applicationDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? string.Empty;
        var certificatePath = Path.Combine(applicationDirectory, "raven-client-certificate.pfx");

        if (File.Exists(certificatePath))
        {
            return new X509Certificate2(certificatePath, certInfo.ClientCertificatePassword);
        }
        return null;
    }
}

public interface IRavenClientCertificateInfo
{
    string? ClientCertificatePath { get; }
    string? ClientCertificateBase64 { get; }
    string? ClientCertificatePassword { get; }
}