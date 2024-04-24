namespace Particular.ThroughputCollector.Shared;

using System.Security.Cryptography;
using System.Text.Json;
using Particular.ThroughputCollector.Contracts;

public static class Signature
{
    static readonly string publicKeyText;

    static Signature()
    {
        var assembly = typeof(Signature).Assembly;
        var assemblyName = assembly.GetName().Name;
        using (var stream = assembly.GetManifestResourceStream($"{assemblyName}.Shared.public-key.pem"))
        using (var reader = new StreamReader(stream!))
        {
            publicKeyText = reader.ReadToEnd();
        }
    }

    public static string SignReport(Report report)
    {
        var bytesToSign = JsonSerializer.SerializeToUtf8Bytes(report, SerializationOptions.SerializeNotIndented);

        using (var rsa = RSA.Create())
        using (var sha = SHA512.Create())
        {
            rsa.ImportFromPem(publicKeyText);

            var hash = sha.ComputeHash(bytesToSign);

            var signature = rsa.Encrypt(hash, RSAEncryptionPadding.Pkcs1);

            return Convert.ToBase64String(signature);
        }
    }
}