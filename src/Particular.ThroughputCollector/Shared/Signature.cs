namespace Particular.ThroughputCollector.Shared;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Particular.ThroughputCollector.Contracts;

public static class Signature
{
    static readonly string publicKeyText;

    static Signature()
    {
        var assembly = typeof(Signature).Assembly;
        var assemblyName = assembly.GetName().Name;
        using (var stream = assembly.GetManifestResourceStream($"{assemblyName}.public-key.pem"))
        using (var reader = new StreamReader(stream!))
        {
            publicKeyText = reader.ReadToEnd();
        }
    }

    public static string SignReport(Report report)
    {
        var options = new JsonSerializerOptions()
        {
            WriteIndented = false
        };
        var jsonToSign = JsonSerializer.Serialize(report, options);

        var bytesToSign = Encoding.UTF8.GetBytes(jsonToSign);

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