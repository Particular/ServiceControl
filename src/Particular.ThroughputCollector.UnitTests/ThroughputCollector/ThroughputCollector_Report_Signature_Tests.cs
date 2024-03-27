namespace Particular.ThroughputCollector.UnitTests;

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NUnit.Framework;
using Particular.Approvals;
using Particular.ThroughputCollector.Contracts;

[TestFixture]
public class ThroughputCollector_Report_Signature_Tests
{
    [TestCase("Serialized")]
    [TestCase("Deserialized")]
    public void SignatureRoundTrip(string scenario)
    {
        var report = CreateReport();

        var reportString = SerializeReport(report);
        if (scenario == "Serialized")
        {
            Approver.Verify(reportString,
                scrubber: input => input.Replace(report.Signature, "SIGNATURE", StringComparison.OrdinalIgnoreCase),
                scenario: scenario);
        }

        if (scenario == "Deserialized")
        {
            var deserialized = DeserializeReport(reportString);

            Approver.Verify(reportString,
                scrubber: input => input.Replace(report.Signature, "SIGNATURE"),
                scenario: scenario);

            Assert.That(ValidateReport(deserialized));
        }
    }

    [Test]
    public void TamperCheck()
    {
        var report = CreateReport();
        var reportString = SerializeReport(report);

        reportString = reportString.Replace("\"Throughput\": 42", "\"Throughput\": 13");

        var deserialized = DeserializeReport(reportString);

        if (PrivateKeyAvailable)
        {
            Assert.That(ValidateReport(deserialized), Is.False);
        }
    }

    [Test]
    public void BunchOfReports()
    {
        var random = new Random();
        var failures = 0;

        for (var i = 0; i < 100; i++)
        {
            var queues = Enumerable.Range(0, 10)
                .Select(_ => new QueueThroughput { QueueName = Guid.NewGuid().ToString(), Throughput = random.Next(0, 10000) })
                .ToArray();

            var report = new Report
            {
                CustomerName = Guid.NewGuid().ToString(),
                MessageTransport = Guid.NewGuid().ToString(),
                ReportMethod = Guid.NewGuid().ToString(),
                ToolVersion = new Version(random.Next(1, 100), random.Next(1, 100), random.Next(1, 100)).ToString(),
                StartTime = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero).AddSeconds(random.Next(0, 31_536_000)),
                EndTime = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero).AddSeconds(random.Next(0, 31_536_000)),
                Queues = queues,
                TotalQueues = queues.Length,
                TotalThroughput = queues.Sum(q => q.Throughput ?? 0)
            };

            var signed = new SignedReport
            {
                ReportData = report,
                Signature = Shared.Signature.SignReport(report)
            };

            try
            {
                _ = ValidateReport(signed);
            }
            catch (CryptographicException x)
            {
                failures++;
                Console.WriteLine($"Failure {failures}:");
                Console.WriteLine(x);
            }
        }

        Assert.That(failures, Is.EqualTo(0));
    }

    [Test]
    public void CanReadV1Report()
    {
        var reportString = GetResource("throughput-report-v1.0.json");

        var report = DeserializeReport(reportString);
        var data = report.ReportData;

        // Want to be explicit with asserts to ensure that a 1.0 report can be read correctly
        // An approval test would be too easy to just accept changes on
        Assert.That(data.CustomerName, Is.EqualTo("Testing"));
        Assert.That(data.MessageTransport, Is.EqualTo("RabbitMQ"));
        Assert.That(data.ReportMethod, Is.EqualTo("ThroughputTool: RabbitMQ Admin"));
        Assert.That(data.ToolVersion, Is.EqualTo("1.0.0"));
        Assert.That(data.StartTime.ToString("O"), Is.EqualTo("2022-11-01T10:58:55.5665172-05:00"));
        Assert.That(data.EndTime.ToString("O"), Is.EqualTo("2022-11-01T10:59:55.6677584-05:00"));
        Assert.That(data.ReportDuration, Is.EqualTo(TimeSpan.Parse("00:01:00.1012412")));

        Assert.That(data.Queues, Has.Length.EqualTo(7));
        Assert.That(data.Queues.All(q => q.Throughput == 0));
        Assert.That(data.Queues.All(q => !string.IsNullOrEmpty(q.QueueName)));
        Assert.That(data.Queues.All(q => q.NoDataOrSendOnly == false));
        Assert.That(data.Queues.All(q => q.EndpointIndicators is null));

        Assert.That(data.TotalThroughput, Is.EqualTo(0));
        Assert.That(data.TotalQueues, Is.EqualTo(7));

        Assert.That(report.Signature, Is.EqualTo("ybIzoo9ogZtbSm5+jJa3GxncjCX3fxAfiLSI7eogG20KjJiv43aCE+7Lsvhkat7AALM34HgwI3VsgzRmyLYXD5n0+XRrWXNgeRGbLEG6d1W2djLRHNjXo423zpGTYDeMq3vhI9yAcil0K0dCC/ZCnw8dPd51pNmgKYIvrfELW0hyN70trUeCMDhYRfXruWLNe8Hfy+tS8Bm13B5vknXNlAjBIuGjXn3XILRRSVrTbb4QMIRzSluSnSTFPTCyE9wMWwC0BUGSf7ZEA0XdeN6UkaO/5URSOQVesiSLRqQWbfUc87XlY1hMs5Z7kLSOr5WByIQIfQKum1nGVjLMzshyhQ=="));

        Assert.That(ValidateReport(report));
    }

    string GetResource(string resourceName)
    {
        var assembly = typeof(ThroughputCollector_Report_Signature_Tests).Assembly;
        var assemblyName = assembly.GetName().Name;
        using (var stream = assembly.GetManifestResourceStream($"{assemblyName}.ThroughputCollector.{resourceName}"))
        using (var reader = new StreamReader(stream))
        {
            return reader.ReadToEnd();
        }
    }

    SignedReport CreateReport()
    {
        var start = new DateTimeOffset(2022, 09, 01, 0, 0, 0, TimeSpan.Zero);
        var end = start.AddDays(1);

        var queues = new[]
        {
            new QueueThroughput { QueueName = "Queue1", Throughput = 42 },
            new QueueThroughput { QueueName = "Queue1", Throughput = 10000 },
            new QueueThroughput { QueueName = "NoData", NoDataOrSendOnly = true },
        };

        var reportData = new Report
        {
            CustomerName = "Test",
            MessageTransport = "Fake Transport",
            ReportMethod = "Testing",
            ToolVersion = "1.0.0",
            StartTime = start,
            EndTime = end,
            ReportDuration = end - start,
            Queues = queues,
            TotalThroughput = queues.Sum(q => q.Throughput ?? 0),
            TotalQueues = queues.Length,
            Prefix = "SomePrefix",
            IgnoredQueues = new[] { "ignore1", "ignore2", "ignore3" }
        };

        return new SignedReport
        {
            ReportData = reportData,
            Signature = Shared.Signature.SignReport(reportData)
        };
    }

    string SerializeReport(SignedReport report)
    {
        var options = new JsonSerializerOptions()
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        var stringReport = JsonSerializer.Serialize(report, options);

        return stringReport;
    }

    SignedReport DeserializeReport(string reportString)
    {
        return JsonSerializer.Deserialize<SignedReport>(reportString);
    }

    bool PrivateKeyAvailable => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RSA_PRIVATE_KEY"));

    bool ValidateReport(SignedReport signedReport)
    {
        if (signedReport.Signature is null)
        {
            return false;
        }

#if DEBUG
        if (!PrivateKeyAvailable)
        {
            // We don't distribute the private key to do local testing, this only happens during CI
            Console.WriteLine("Ignoring report validation as this is a DEBUG build and the RSA_PRIVATE_KEY environment variable is missing.");
            return true;
        }
#endif

        if (PrivateKeyAvailable) //TODO get rid of this check after introducing env variable to CI
        {
            var options = new JsonSerializerOptions()
            {
                WriteIndented = false,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            var reserializedReportBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(signedReport.ReportData, options));
            var shaHash = GetShaHash(reserializedReportBytes);

            try
            {
                using (var rsa = RSA.Create())
                {
                    var privateKeyText = Environment.GetEnvironmentVariable("RSA_PRIVATE_KEY");
                    ImportPrivateKey(rsa, privateKeyText);

                    var correctSignature = Convert.ToBase64String(shaHash);

                    var decryptedHash = rsa.Decrypt(Convert.FromBase64String(signedReport.Signature), RSAEncryptionPadding.Pkcs1);
                    var decryptedSignature = Convert.ToBase64String(decryptedHash);

                    return correctSignature == decryptedSignature;
                }
            }
            catch (CryptographicException)
            {
                // The signature was invalid and couldn't be decrypted
                return false;
            }
        }

        return true;
    }

    byte[] GetShaHash(byte[] reportBytes)
    {
        using (var sha = SHA512.Create())
        {
            return sha.ComputeHash(reportBytes);
        }
    }

    static void ImportPrivateKey(RSA rsa, string privateKeyText)
    {
        rsa.ImportFromPem(privateKeyText);
    }
}