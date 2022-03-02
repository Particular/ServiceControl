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
                yield return new TestCaseData("", new ConnectionSettings("test"));
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
