namespace ServiceControl.Audit.UnitTests.SagaAudit
{
    using NUnit.Framework;
    using ServiceControl.SagaAudit;
    using JsonSerializer = System.Text.Json.JsonSerializer;

    [TestFixture]
    public class CustomTimeSpanConverterTests
    {
        [Theory]
        [TestCase("2:0:00:00")]
        [TestCase("1:00:00")]
        [TestCase("1")]
        [TestCase("10")]
        [TestCase("00:01")]
        [TestCase("0:00:02")]
        [TestCase("0:00:00.0000001")]
        [TestCase("0:00:00.0000010")]
        [TestCase("0:00:00.0000100")]
        [TestCase("0:00:00.0001000")]
        [TestCase("0:00:00.0010000")]
        [TestCase("0:00:00.0100000")]
        [TestCase("0:00:00.1000000")]
        [TestCase("23:59:59")]
        [TestCase("\\u002D23:59:59")]
        [TestCase("\\u0032\\u0033\\u003A\\u0035\\u0039\\u003A\\u0035\\u0039")]
        [TestCase("23:59:59.9")]
        [TestCase("23:59:59.9999999")]
        [TestCase("9999999.23:59:59.9999999")]
        [TestCase("-9999999.23:59:59.9999999")]
        [TestCase("10675199.02:48:05.4775807")] // TimeSpan.MaxValue
        [TestCase("-10675199.02:48:05.4775808")] // TimeSpan.MinValue
        public void Should_not_throw_on_valid_timespans(string timespan)
        {
            string json = $$"""
                            {
                                "ResultingMessages": [
                                    {
                                        "DeliveryAt": null,
                                        "DeliveryDelay": "{{timespan}}"
                                    }
                                ]
                            }
                            """;

            Assert.DoesNotThrow(() =>
            {
                JsonSerializer.Deserialize(json, SagaAuditMessagesSerializationContext.Default.SagaUpdatedMessage);
            });
        }

        [Test]
        public void Should_not_throw_on_null()
        {
            string json = """
                          {
                              "ResultingMessages": [
                                  {
                                      "DeliveryAt": null,
                                      "DeliveryDelay":null
                                  }
                              ]
                          }
                          """;

            Assert.DoesNotThrow(() =>
            {
                JsonSerializer.Deserialize(json, SagaAuditMessagesSerializationContext.Default.SagaUpdatedMessage);
            });
        }
    }
}