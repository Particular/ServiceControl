namespace Particular.LicensingComponent.UnitTests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using NUnit.Framework;
using Particular.Approvals;
using Particular.LicensingComponent.Contracts;
using Particular.LicensingComponent.Shared;

[TestFixture]
public class ThroughputCollector_Report_Signature_Tests
{
    [Test]
    public void Should_serialize_report_with_signature()
    {
        //Arrange
        var report = CreateReport();

        //Act
        var reportString = JsonSerializer.Serialize(report, SerializationOptions.IndentedWithNoEscaping);

        //Assert
        Approver.Verify(reportString,
            scrubber: input => input.Replace(report.Signature, "SIGNATURE"));
    }

    [Test]
    public void Should_deserialize_report_with_signature()
    {
        //Arrange
        var report = CreateReport();

        //Act
        var reportString = JsonSerializer.Serialize(report, SerializationOptions.NotIndentedWithNoEscaping);
        var deserialized = JsonSerializer.Deserialize<SignedReport>(reportString, SerializationOptions.NotIndentedWithNoEscaping);

        //Assert
        Assert.That(ValidateReport(deserialized));
    }

    [Test]
    public void Should_not_allow_tempering_with_report()
    {
        //Arrange
        var report = CreateReport();
        var reportString = JsonSerializer.Serialize(report, SerializationOptions.IndentedWithNoEscaping);

        //Act
        reportString = reportString.Replace("\"Throughput\": 42", "\"Throughput\": 13");
        var deserialized = JsonSerializer.Deserialize<SignedReport>(reportString, SerializationOptions.NotIndentedWithNoEscaping);

        //Assert
        Assert.That(ValidateReport(deserialized), Is.False);
    }

    [Test]
    public void Should_be_able_to_read_a_V1_report()
    {
        //Arrange
        var reportString = GetResource("throughput-report-v1.0.json");

        //Act
        var report = JsonSerializer.Deserialize<SignedReport>(reportString, SerializationOptions.NotIndentedWithNoEscaping);
        var data = report.ReportData;

        //Assert
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
        Assert.That(data.Queues.All(q => !q.NoDataOrSendOnly));
        Assert.That(data.Queues.All(q => q.EndpointIndicators is null));

        Assert.That(data.TotalThroughput, Is.EqualTo(0));
        Assert.That(data.TotalQueues, Is.EqualTo(7));

        Assert.That(report.Signature, Is.EqualTo("ybIzoo9ogZtbSm5+jJa3GxncjCX3fxAfiLSI7eogG20KjJiv43aCE+7Lsvhkat7AALM34HgwI3VsgzRmyLYXD5n0+XRrWXNgeRGbLEG6d1W2djLRHNjXo423zpGTYDeMq3vhI9yAcil0K0dCC/ZCnw8dPd51pNmgKYIvrfELW0hyN70trUeCMDhYRfXruWLNe8Hfy+tS8Bm13B5vknXNlAjBIuGjXn3XILRRSVrTbb4QMIRzSluSnSTFPTCyE9wMWwC0BUGSf7ZEA0XdeN6UkaO/5URSOQVesiSLRqQWbfUc87XlY1hMs5Z7kLSOr5WByIQIfQKum1nGVjLMzshyhQ=="));

        Assert.That(ValidateReport(report));
    }

    [Test]
    public void Should_be_able_to_read_a_V2_report()
    {
        //Arrange
        var reportString = GetResource("throughput-report-v2.0.json");

        //Act
        var report = JsonSerializer.Deserialize<SignedReport>(reportString, SerializationOptions.NotIndentedWithNoEscaping);
        var data = report.ReportData;

        //Assert
        // Want to be explicit with asserts to ensure that a 2.0 report can be read correctly
        // An approval test would be too easy to just accept changes on
        Assert.That(data.CustomerName, Is.EqualTo("TestCustomer"));
        Assert.That(data.MessageTransport, Is.EqualTo("AzureServiceBus"));
        Assert.That(data.ReportMethod, Is.EqualTo("Broker"));
        Assert.That(data.ToolType, Is.EqualTo("Platform Licensing Component"));
        Assert.That(data.ToolVersion, Is.EqualTo("5.0.1"));
        Assert.That(data.StartTime.ToString("O"), Is.EqualTo("2024-04-24T00:00:00.0000000+00:00"));
        Assert.That(data.EndTime.ToString("O"), Is.EqualTo("2024-04-25T00:00:00.0000000+00:00"));
        Assert.That(data.ReportDuration, Is.EqualTo(TimeSpan.Parse("1.00:00:00")));

        Assert.That(data.Queues, Has.Length.EqualTo(5));
        Assert.That(data.Queues.All(q => !string.IsNullOrEmpty(q.QueueName)));
        Assert.That(data.Queues.All(q => !q.NoDataOrSendOnly));
        Assert.That(data.Queues.Any(q => q.QueueName == "Endpoint2" && q.DailyThroughputFromBroker.Any(t => t.MessageCount == 60 && t.DateUTC.ToString("yyyy-MM-dd") == "2024-04-24")));
        Assert.That(data.Queues.Any(q => q.EndpointIndicators?.Contains(EndpointIndicator.KnownEndpoint.ToString()) ?? false), Is.True);

        Assert.That(data.TotalThroughput, Is.EqualTo(249));
        Assert.That(data.TotalQueues, Is.EqualTo(5));
        Assert.That(data.EnvironmentInformation.AuditServicesData.Versions.Count, Is.EqualTo(1));
        Assert.That(data.EnvironmentInformation.AuditServicesData.Transports.Count, Is.EqualTo(1));
        Assert.That(data.EnvironmentInformation.EnvironmentData.ContainsKey(EnvironmentDataType.ServiceControlVersion.ToString()), Is.True);
        Assert.That(data.EnvironmentInformation.EnvironmentData[EnvironmentDataType.ServiceControlVersion.ToString()], Is.EqualTo("5.0.1"));
        Assert.That(data.EnvironmentInformation.EnvironmentData.ContainsKey(EnvironmentDataType.ServicePulseVersion.ToString()), Is.True);
        Assert.That(data.EnvironmentInformation.EnvironmentData[EnvironmentDataType.ServicePulseVersion.ToString()], Is.EqualTo("2.3.1"));
        Assert.That(data.EnvironmentInformation.EnvironmentData.ContainsKey(EnvironmentDataType.AuditEnabled.ToString()), Is.True);
        Assert.That(data.EnvironmentInformation.EnvironmentData[EnvironmentDataType.AuditEnabled.ToString()], Is.EqualTo("True"));
        Assert.That(data.EnvironmentInformation.EnvironmentData.ContainsKey(EnvironmentDataType.MonitoringEnabled.ToString()), Is.True);
        Assert.That(data.EnvironmentInformation.EnvironmentData[EnvironmentDataType.MonitoringEnabled.ToString()], Is.EqualTo("True"));

        Assert.That(report.Signature, Is.EqualTo("IEbO4i0Jn54iHUzlwotHf9aw/fZIHY+dztY9cMRkWjVVo6AiYtihWR0mip793gRrWHOxHVobCpa4l5svRk16mBR+YAOrs3KNRVTzrl4+wL21e1u9zFuPNrHLtFeul+taJxV8ciA7zEgD7LMle9CcR/Vfm8BZ9mmD5W/DjsCYLCdVXfN4iRMlz+eW50mOHty21yJ0pOiYBooaN2EJexVY4Q+5FMyAkm0wucEPFyaQB6+SfcS37fEm807B7sXhtUPiW+einqDOX6uYF+MuXxUn1u9LxlEWKV9kPqXJnulxmoReHXHigP45pj/8m9jUzrQdagINl1uIOBkq5SMDccRfTA=="));

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

        //Arrange
        var reportDataString = File.ReadAllText(reportFile);
        var reportData = JsonSerializer.Deserialize<Report>(reportDataString, SerializationOptions.NotIndentedWithNoEscaping);

        //Act
        var signedReport = new SignedReport
        {
            ReportData = reportData,
            Signature = Signature.SignReport(reportData)
        };

        var reportString = JsonSerializer.Serialize(signedReport, SerializationOptions.IndentedWithNoEscaping);

        //Assert
        Assert.That(reportString, Is.Not.Null);
        Assert.That(signedReport.Signature, Is.Not.Null);
    }

#if !DEBUG
    [Ignore("This test is here to validate generated reports")]
#endif
    [TestCase(@"C:\DEV\ThroughputReports\ReportDataWithSignature.json")]
    public void ValidateReport(string reportFile)
    {
        if (!File.Exists(reportFile))
        {
            Assert.Ignore($"Ignoring ValidateReport test as test report data file {reportFile} was not found.");
        }

        //Arrange
        var reportString = File.ReadAllText(reportFile);
        var report = JsonSerializer.Deserialize<SignedReport>(reportString, SerializationOptions.NotIndentedWithNoEscaping);

        //Assert
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
            IgnoredQueues = new[] { "ignore1", "ignore2", "ignore3" },
            EnvironmentInformation = new EnvironmentInformation { EnvironmentData = new Dictionary<string, string> { [EnvironmentDataType.BrokerVersion.ToString()] = "1.2.0" } }
        };

        return new SignedReport
        {
            ReportData = reportData,
            Signature = Signature.SignReport(reportData)
        };
    }

    bool ValidateReport(SignedReport signedReport)
    {
        if (signedReport.Signature is null)
        {
            return false;
        }

#if DEBUG
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RSA_PRIVATE_KEY")))
        {
            // We don't distribute the private key to do local testing, this only happens during CI
            Assert.Ignore("Ignoring report validation as this is a DEBUG build and the RSA_PRIVATE_KEY environment variable is missing.");
            return true;
        }
#endif

        var reserializedReportBytes = JsonSerializer.SerializeToUtf8Bytes(signedReport.ReportData, SerializationOptions.NotIndentedWithNoEscaping);
        var correctSignature = Convert.ToBase64String(GetShaHash(reserializedReportBytes));

        try
        {
            using (var rsa = RSA.Create())
            {
                var privateKeyText = Environment.GetEnvironmentVariable("RSA_PRIVATE_KEY");

                ImportPrivateKey(rsa, privateKeyText);

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

    void ImportPrivateKey(RSA rsa, string privateKeyText)
    {
        rsa.ImportFromPem(privateKeyText);
    }


}