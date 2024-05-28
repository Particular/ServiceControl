namespace ServiceControl.Audit.UnitTests.SagaAudit
{
    using NUnit.Framework;
    using ServiceControl.SagaAudit;
    using JsonSerializer = System.Text.Json.JsonSerializer;

    [TestFixture]
    public class CustomTimeSpanConverterTests
    {
        [Test]
        // Days, Hours, Minutes, and Seconds
        [TestCase("1:0:00:00")] // 1 day, 0 hours, 0 minutes, 0 seconds
        [TestCase("2:0:00:00")] // 2 days, 0 hours, 0 minutes, 0 seconds
        [TestCase("3:5:6:7.890")] // 3 days, 5 hours, 6 minutes, 7 seconds, with milliseconds
        [TestCase("1:02:03")] // 1 hour, 2 minutes, 3 seconds
        [TestCase("0:00:00")] // Zero TimeSpan
        [TestCase("1.0:00:00")] // 1 day, 0 hours, 0 minutes, 0 seconds
        [TestCase("1.23:59:59")] // 1 day, 23 hours, 59 minutes, 59 seconds
        [TestCase("10.12:30:45")] // 10 days, 12 hours, 30 minutes, 45 seconds

        // Milliseconds and Ticks
        [TestCase("0:00:00.123")] // 123 milliseconds
        [TestCase("0:00:00.9999999")] // 9999999 ticks
        [TestCase("0:00:00.1")] // 1 tenth of a second
        [TestCase("1.0:00:00.1234567")] // 1 day, 0 hours, 0 minutes, 0 seconds, with fractional seconds

        // Single Time Components
        [TestCase("1:00:00")] // 1 hour, 0 minutes, 0 seconds
        [TestCase("1:2:3")] // 1 hour, 2 minutes, 3 seconds without leading zeros
        [TestCase("0:1:1")] // Zero hours with 1 minute and 1 second
        [TestCase("1:1:1.123")] // 1 hour, 1 minute, 1 second with milliseconds
        [TestCase("0:0:0.1")] // 1 tenth of a second
        [TestCase("1:2:3.456")] // 1 hour, 2 minutes, 3 seconds with milliseconds
        [TestCase("12:34:56.789")] // 12 hours, 34 minutes, 56 seconds with milliseconds
        [TestCase("0:59:59")] // 59 minutes, 59 seconds
        [TestCase("0:0:0.999")] // 999 milliseconds
        [TestCase("6:30:0")] // 6 hours, 30 minutes

        // Whole Days
        [TestCase("1")] // 1 day
        [TestCase("10")] // 10 days

        // Minutes and Seconds
        [TestCase("00:01")] // 1 minute
        [TestCase("0:00:02")] // 2 seconds

        // Small Fractions of a Second
        [TestCase("0:00:00.0000001")] // 1 tick
        [TestCase("0:00:00.0000010")] // 10 ticks
        [TestCase("0:00:00.0000100")] // 100 ticks
        [TestCase("0:00:00.0001000")] // 1000 ticks
        [TestCase("0:00:00.0010000")] // 10000 ticks (1 millisecond)
        [TestCase("0:00:00.0100000")] // 100000 ticks (10 milliseconds)
        [TestCase("0:00:00.1000000")] // 1000000 ticks (100 milliseconds)

        // Large Time Values & Special chars
        [TestCase("23:59:59")] // 23 hours, 59 minutes, 59 seconds
        [TestCase("\\u002D23:59:59")] // -23 hours, 59 minutes, 59 seconds (Unicode escape for minus sign)
        [TestCase(
            "\\u0032\\u0033\\u003A\\u0035\\u0039\\u003A\\u0035\\u0039")] // "23:59:59" with Unicode escape sequences for digits and colon
        [TestCase("23:59:59.9")] // 23 hours, 59 minutes, 59 seconds, with 900 milliseconds
        [TestCase("23:59:59.9999999")] // 23 hours, 59 minutes, 59 seconds, with 9999999 ticks
        [TestCase("9999999.23:59:59.9999999")] // 9999999 days, 23 hours, 59 minutes, 59 seconds, with 9999999 ticks
        [TestCase("-9999999.23:59:59.9999999")] // -9999999 days, 23 hours, 59 minutes, 59 seconds, with 9999999 ticks

        // Max and Min TimeSpan Values
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