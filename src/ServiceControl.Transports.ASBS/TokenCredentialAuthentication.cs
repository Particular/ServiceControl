namespace ServiceControl.Transports.ASBS
{
    using Azure.Core;
    using Azure.Messaging.ServiceBus.Administration;
    using NServiceBus;

    public class TokenCredentialAuthentication : AuthenticationMethod
    {
        public TokenCredentialAuthentication(string fullyQualifiedNamespace, TokenCredential credential)
        {
            FullyQualifiedNamespace = fullyQualifiedNamespace;
            Credential = credential;
        }

        public string FullyQualifiedNamespace { get; }

        public TokenCredential Credential { get; }

        public override ServiceBusAdministrationClient BuildManagementClient()
            => new ServiceBusAdministrationClient(FullyQualifiedNamespace, Credential);

        public override void ConfigureConnection(TransportExtensions<AzureServiceBusTransport> transport)
        {
            transport.ConnectionString(FullyQualifiedNamespace);
            transport.CustomTokenCredential(Credential);
        }
    }
}