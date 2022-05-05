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
                    new ConnectionSettings(new TokenCredentialAuthentication("some.endpoint.name", new DefaultAzureCredential())));
                //Endpoint
                yield return new TestCaseData("Endpoint=sb://some.endpoint.name/",
                    new ConnectionSettings(new SharedAccessSignatureAuthentication("Endpoint=sb://some.endpoint.name/")));
                //Managed identity enabled
                yield return new TestCaseData("Endpoint=sb://some.endpoint.name/;Authentication=Managed Identity",
                    new ConnectionSettings(new TokenCredentialAuthentication("some.endpoint.name", new ManagedIdentityCredential())));
                //Managed identity, user-assigned
                yield return new TestCaseData("Endpoint=sb://some.endpoint.name/;Authentication=Managed Identity;ClientId=ABC",
                    new ConnectionSettings(new TokenCredentialAuthentication("some.endpoint.name", new ManagedIdentityCredential("ABCB"))));
                //Managed identity, custom topic
                yield return new TestCaseData("Endpoint=sb://some.endpoint.name/;Authentication=Managed Identity;TopicName=my_topic;",
                    new ConnectionSettings(new TokenCredentialAuthentication("some.endpoint.name", new ManagedIdentityCredential()), topicName: "my_topic"));
                //Managed identity, web sockets
                yield return new TestCaseData("Endpoint=sb://some.endpoint.name/;Authentication=Managed Identity;TransportType=AmqpWebSockets",
                    new ConnectionSettings(new TokenCredentialAuthentication("some.endpoint.name", new ManagedIdentityCredential()), useWebSockets: true));
                //Managed identity, custom query delay interval
                yield return new TestCaseData("Endpoint=sb://some.endpoint.name/;Authentication=Managed Identity;QueueLengthQueryDelayInterval=15000",
                    new ConnectionSettings(new TokenCredentialAuthentication("some.endpoint.name", new ManagedIdentityCredential()), queryDelayInterval: TimeSpan.FromSeconds(15)));
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
                //Fully-qualified namespace with trailing slash
                yield return new TestCaseData("some.endpoint.name/");
                //Non int query delay
                yield return new TestCaseData("Endpoint=sb://some.endpoint.name/;QueueLengthQueryDelayInterval=not int");
            }
        }

        [TestCaseSource("SupportedConnectionStrings")]
        public void VerifySupported(string connectionString, ConnectionSettings expected)
        {
            var actual = ConnectionStringParser.Parse(connectionString);

            Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));

            //needed since System..Text.Json doesn't handle polymorphic properties
            Assert.AreEqual(
                JsonSerializer.Serialize(expected.AuthenticationMethod, expected.AuthenticationMethod.GetType()),
                JsonSerializer.Serialize(actual.AuthenticationMethod, actual.AuthenticationMethod.GetType()));


            //needed since System..Text.Json doesn't handle polymorphic properties
            if (expected.AuthenticationMethod is TokenCredentialAuthentication expectedAuthentication)
            {
                var actualAuthentication = actual.AuthenticationMethod as TokenCredentialAuthentication;

                Assert.NotNull(actualAuthentication);
                Assert.IsInstanceOf(expectedAuthentication.Credential.GetType(), actualAuthentication.Credential);
            }
        }

        [TestCaseSource("NotSupportedConnectionStrings")]
        public void VerifyNotSupported(string connectionString)
        {
            Assert.Throws<Exception>(() => ConnectionStringParser.Parse(connectionString));
        }
    }
}
