#nullable enable
namespace ServiceControl.Transports.ASBS
{
    using Azure.Core;
    using Azure.Identity;
    using Azure.Messaging.ServiceBus.Administration;
    using NServiceBus;

    public class TokenCredentialAuthentication : AuthenticationMethod
    {
        public TokenCredentialAuthentication(string fullyQualifiedNamespace)
        {
            FullyQualifiedNamespace = fullyQualifiedNamespace;
            Credential = new DefaultAzureCredential();
        }

        public TokenCredentialAuthentication(string fullyQualifiedNamespace, string? clientId)
        {
            FullyQualifiedNamespace = fullyQualifiedNamespace;
            ClientId = clientId;
            Credential = new ManagedIdentityCredential(clientId is not null ? ManagedIdentityId.FromUserAssignedClientId(clientId) : ManagedIdentityId.SystemAssigned);
        }

        public string FullyQualifiedNamespace { get; }

        public TokenCredential Credential { get; }

        public string? ClientId { get; }

        public override ServiceBusAdministrationClient BuildManagementClient(ServiceBusAdministrationClientOptions? options = null)
            => options is null
                ? new(FullyQualifiedNamespace, Credential)
                : new(FullyQualifiedNamespace, Credential, options);

        public override AzureServiceBusTransport CreateTransportDefinition(ConnectionSettings connectionSettings, TopicTopology topology)
        {
            var transport = new AzureServiceBusTransport(FullyQualifiedNamespace, Credential, topology);
            return transport;
        }
    }
}