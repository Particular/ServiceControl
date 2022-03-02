namespace ServiceControl.Transports.UnitTests.ASBS
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using Azure.Identity;
    using NUnit.Framework;
    using ServiceControl.Transports.ASBS;

    [TestFixture]
    class ConnectionStringParserTests
    {
        public static IEnumerable<TestCaseData> SupportedConnectionStrings
        {
            get
            {
                //Just the fully-qualified namespace - forces managed identity
                yield return new TestCaseData("some.endpoint.name",
                    new ConnectionSettings("some.endpoint.name", false, "some.endpoint.name", useDefaultCredentials: true));
                //Endpoint
                yield return new TestCaseData("Endpoint=sb://some.endpoint.name/",
                    new ConnectionSettings("Endpoint=sb://some.endpoint.name/", false, "some.endpoint.name"));
                //Managed identity enabled
                yield return new TestCaseData("Endpoint=sb://some.endpoint.name/;Authentication=Managed Identity",
                    new ConnectionSettings("some.endpoint.name", true, "some.endpoint.name"));
                //Managed identity, user-assigned
                yield return new TestCaseData("Endpoint=sb://some.endpoint.name/;Authentication=Managed Identity;ClientId=ABC",
                    new ConnectionSettings("some.endpoint.name", true, "some.endpoint.name", "ABC"));
                //Managed identity, custom topic
                yield return new TestCaseData("Endpoint=sb://some.endpoint.name/;Authentication=Managed Identity;TopicName=my_topic",
                    new ConnectionSettings("some.endpoint.name", true, "some.endpoint.name", topicName: "my_topic"));
                //Managed identity, web sockets
                yield return new TestCaseData("Endpoint=sb://some.endpoint.name/;Authentication=Managed Identity;TransportType=AmqpWebSockets",
                    new ConnectionSettings("some.endpoint.name", true, "some.endpoint.name", useWebSockets: true));
            }
        }

        public static IEnumerable<TestCaseData> NotSupportedConnectionStrings
        {
            get
            {
                //Client id but managed identity not enabled
                yield return new TestCaseData("Endpoint=sb://some.endpoint.name/;ClientId=false");
                //Managed identity but no endpoint
                yield return new TestCaseData("Authentication=Managed Identity");
                //Fully-qualified namespace with prefix
                yield return new TestCaseData("sb://some.endpoint.name/");
            }
        }

        [TestCaseSource("SupportedConnectionStrings")]
        public void VerifySupported(string connectionString, ConnectionSettings expected)
        {
            var actual = new ConnectionStringParser().Parse(connectionString);

            Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        }

        [TestCaseSource("NotSupportedConnectionStrings")]
        public void VerifyNotSupported(string connectionString)
        {
            Assert.Throws<Exception>(() => new ConnectionStringParser().Parse(connectionString));
        }
    }
}
