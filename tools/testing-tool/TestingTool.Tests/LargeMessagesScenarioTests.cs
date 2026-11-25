using System.Text;
using System.Text.Json;
using NUnit.Framework;
using TestingTool.Scenarios;

namespace TestingTool.Tests;

public class LargeMessagesScenarioTests
{
    const int ExternalBodyStorageThresholdBytes = 100 * 1024;
    const int AzureServiceBusStandardLimitBytes = 256 * 1024;
    const int MinimumHeaderHeadroomBytes = 56 * 1024;

    [Test]
    public void Serialized_body_exceeds_external_storage_threshold_and_leaves_header_headroom()
    {
        var scenario = new LargeMessagesScenario("test");
        var message = new LoadMessage { Sequence = 1, TextBody = MessageTextGenerator.GenerateBody(1, scenario.BodySizeBytes) };

        var serializedBytes = Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(message));

        Assert.That(serializedBytes, Is.GreaterThan(ExternalBodyStorageThresholdBytes));
        Assert.That(serializedBytes, Is.LessThanOrEqualTo(AzureServiceBusStandardLimitBytes - MinimumHeaderHeadroomBytes));
    }
}
