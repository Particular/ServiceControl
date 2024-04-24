﻿namespace Particular.ThroughputCollector.UnitTests;

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
    [Test]
    public void Should_serialize_report_with_signature()
    {
        var report = CreateReport();

        var reportString = SerializeReport(report);

        Approver.Verify(reportString,
            scrubber: input => input.Replace(report.Signature, "SIGNATURE"));
    }

    [Test]
    public void Should_deserialize_report_with_signature()
    {
        var report = CreateReport();

        var reportString = SerializeReport(report);
        var deserialized = DeserializeReport(reportString);

        Approver.Verify(reportString,
            scrubber: input => input.Replace(report.Signature, "SIGNATURE"));

        Assert.That(ValidateReport(deserialized));
    }

    [Test]
    public void Should_not_allow_tempering_with_report()
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
    public void Should_be_able_to_read_a_V1_report()
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

    [Test]
    public void Should_be_able_to_read_a_V2_report()
    {
        var reportString = GetResource("throughput-report-v2.0.json");

        var report = DeserializeReport(reportString);
        var data = report.ReportData;

        // Want to be explicit with asserts to ensure that a 2.0 report can be read correctly
        // An approval test would be too easy to just accept changes on
        Assert.That(data.CustomerName, Is.EqualTo("TestCustomer"));
        Assert.That(data.MessageTransport, Is.EqualTo("AzureServiceBus"));
        Assert.That(data.ReportMethod, Is.EqualTo("NA"));
        Assert.That(data.ToolVersion, Is.EqualTo("2.0.0"));
        Assert.That(data.StartTime.ToString("O"), Is.EqualTo("2024-04-22T00:00:00.0000000+00:00"));
        Assert.That(data.EndTime.ToString("O"), Is.EqualTo("2024-04-23T00:00:00.0000000+00:00"));
        Assert.That(data.ReportDuration, Is.EqualTo(TimeSpan.Parse("1.00:00:00")));

        Assert.That(data.Queues, Has.Length.EqualTo(5));
        Assert.That(data.Queues.All(q => !string.IsNullOrEmpty(q.QueueName)));
        Assert.That(data.Queues.All(q => q.NoDataOrSendOnly == false));
        Assert.That(data.Queues.Any(q => q.EndpointIndicators?.Contains(EndpointIndicator.KnownEndpoint.ToString()) ?? false), Is.True);

        Assert.That(data.TotalThroughput, Is.EqualTo(249));
        Assert.That(data.TotalQueues, Is.EqualTo(5));
        Assert.That(data.EnvironmentInformation.AuditServiceMetadata.Versions.Count, Is.EqualTo(1));
        Assert.That(data.EnvironmentInformation.AuditServiceMetadata.Transports.Count, Is.EqualTo(1));
        Assert.That(data.EnvironmentInformation.EnvironmentData.ContainsKey(EnvironmentDataType.ServiceControlVersion.ToString()), Is.True);
        Assert.That(data.EnvironmentInformation.EnvironmentData[EnvironmentDataType.ServiceControlVersion.ToString()], Is.EqualTo("5.0.1"));
        Assert.That(data.EnvironmentInformation.EnvironmentData.ContainsKey(EnvironmentDataType.ServicePulseVersion.ToString()), Is.True);
        Assert.That(data.EnvironmentInformation.EnvironmentData[EnvironmentDataType.ServicePulseVersion.ToString()], Is.EqualTo("2.3.1"));
        Assert.That(data.EnvironmentInformation.EnvironmentData.ContainsKey(EnvironmentDataType.AuditEnabled.ToString()), Is.True);
        Assert.That(data.EnvironmentInformation.EnvironmentData[EnvironmentDataType.AuditEnabled.ToString()], Is.EqualTo("True"));
        Assert.That(data.EnvironmentInformation.EnvironmentData.ContainsKey(EnvironmentDataType.MonitoringEnabled.ToString()), Is.True);
        Assert.That(data.EnvironmentInformation.EnvironmentData[EnvironmentDataType.MonitoringEnabled.ToString()], Is.EqualTo("True"));

        Assert.That(report.Signature, Is.EqualTo("veu3QRby2Kq3cFLcrhTWyaGqVyLBOANaQ2MC+onthtBEz8AlTebgiG/ASnBkwf5Cg1Q6evq7faRZpbFyxjkKKA1Kz1nfeZc8T9c8jX2Gy4t2XTYtr+Dx/PvCq9pCu/7LrTZRwO+Ndw9a51M2OZpKL07zhP97/IzzcJv+gpdo//L7Haw6iIHhT/IwgwZN4GaTMOVprn5MnS/+9Csh27JMCITFPwpaIm2p7KZz0ycoexaNnxhcXF7vzhAVOzOmtZcp1XJ33TBd2lkQ7v8EWWp7ZXPed7Vgt1+42taa9QRWe0huiBnlp+fWZLFPuD6dbLEjpd9InYETkxsrcKkA2S5vag=="));

        Assert.That(ValidateReport(report));
    }

#if !DEBUG
    [Ignore("This test is here to help with generating a signed report file from a file that only contains report data")]
#endif
    [TestCase(@"C:\DEV\ThroughputReports\ReportData.json")]
    public void SignReport(string reportFile)
    {
        if (!File.Exists(reportFile))
        {
            Assert.Ignore($"Ignoring SignReport test as test report data file {reportFile} was not found.");
        }

        var reportDataString = File.ReadAllText(reportFile);

        var reportData = JsonSerializer.Deserialize<Report>(reportDataString);
        var signedReport = new SignedReport
        {
            ReportData = reportData,
            Signature = Shared.Signature.SignReport(reportData)
        };

        var reportString = SerializeReport(signedReport);
        Assert.That(reportString, Is.Not.Null);
        Assert.That(signedReport.Signature, Is.Not.Null);
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
            Assert.Ignore("Ignoring report validation as this is a DEBUG build and the RSA_PRIVATE_KEY environment variable is missing.");
            return true;
        }
#endif

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