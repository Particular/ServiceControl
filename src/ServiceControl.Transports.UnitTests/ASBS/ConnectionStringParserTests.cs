namespace ServiceControl.Transports.UnitTests.ASBS
{
    using System.Collections.Generic;
    using System.Text.Json;
    using NUnit.Framework;
    using ServiceControl.Transports.ASBS;

    [TestFixture]
    class ConnectionStringParserTests
    {
        public static IEnumerable<TestCaseData> SupportedConnectionStrings
        {
            get
            {
                yield return new TestCaseData("test", new ConnectionSettings("test"));
                yield return new TestCaseData("Endpoint=sb://some.endpoint.name/", new ConnectionSettings("Endpoint=sb://some.endpoint.name/", false, "some.endpoint.name"));
            }
        }

        [TestCaseSource("SupportedConnectionStrings")]
        public void VerifySupported(string connectionString, ConnectionSettings expected)
        {
            var actual = new ConnectionStringParser().Parse(connectionString);

            Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        }
    }
}
