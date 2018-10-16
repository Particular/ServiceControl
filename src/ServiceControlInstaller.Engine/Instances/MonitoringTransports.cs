namespace ServiceControlInstaller.Engine.Instances
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class MonitoringTransports
    {
        public static List<TransportInfo> All => new List<TransportInfo>
        {
            //INFO: Those types are used in the SCMU and in PS scripts. In both cases Match predicate is used to find a transprot info.
            //      In the UI the matching is done based on the transport TypeName from app.config. In PS it's done based on human friendly names.
            //      As a result the Match predicate should evalute to true both for TypeName and human firendly name.
            //      Matching separatelly on Name and TypeName would not be enough because we need to be backwards compatible.
            //      As a result Match is comparing to old/current names, old/current types.
            new TransportInfo
            {
                Name = "AmazonSQS",
                ZipName = "AmazonSQS",
                TypeName = "ServiceControl.Transports.AmazonSQS.ServiceControlSqsTransport, ServiceControl.Transports.AmazonSQS",
                SampleConnectionString = "AccessKeyId=<ACCESSKEYID>;SecretAccessKey=<SECRETACCESSKEY>;Region=<REGION>",
                Help = "AccessKeyId will be promoted to AWS_ACCESS_KEY_ID, SecretAccessKey to AWS_SECRET_ACCESS_KEY and Region to AWS_REGION environment variable.",
                Matches = name => name.Equals("AmazonSQS", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("ServiceControl.Transports.AmazonSQS.ServiceControlSqsTransport, ServiceControl.Transports.AmazonSQS", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("NServiceBus.SqsTransport, NServiceBus.AmazonSQS", StringComparison.OrdinalIgnoreCase)
            },
            new TransportInfo
            {
                Name = "Azure Service Bus - Endpoint-oriented topology (Legacy)",
                ZipName = "AzureServiceBus",
                TypeName = "ServiceControl.Transports.AzureServiceBus.EndpointOrientedTopologyAzureServiceBusTransport, ServiceControl.Transports.AzureServiceBus",
                SampleConnectionString = "Endpoint=sb://[namespace].servicebus.windows.net; SharedSecretIssuer=<owner>;SharedSecretValue=<someSecret>",
                Matches = name => name.Equals("Azure Service Bus - Endpoint-oriented topology (Legacy)", StringComparison.OrdinalIgnoreCase)
                          || name.Equals("ServiceControl.Transports.AzureServiceBus.EndpointOrientedTopologyAzureServiceBusTransport, ServiceControl.Transports.AzureServiceBus", StringComparison.OrdinalIgnoreCase)
            },
            new TransportInfo
            {
                Name = "Azure Service Bus - Forwarding topology (Legacy)",
                ZipName = "AzureServiceBus",
                TypeName = "ServiceControl.Transports.AzureServiceBus.ForwardingTopologyAzureServiceBusTransport, ServiceControl.Transports.AzureServiceBus",
                SampleConnectionString = "Endpoint=sb://[namespace].servicebus.windows.net; SharedSecretIssuer=<owner>;SharedSecretValue=<someSecret>",
                Matches = name => name.Equals("Azure Service Bus - Forwarding topology (Legacy)", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("AzureServiceBus", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("NServiceBus.AzureServiceBusTransport, NServiceBus.Azure.Transports.WindowsAzureServiceBus", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("ServiceControl.Transports.AzureServiceBus.ForwardingTopologyAzureServiceBusTransport, ServiceControl.Transports.AzureServiceBus", StringComparison.OrdinalIgnoreCase)
            },
            new TransportInfo
            {
                Name = "Azure Service Bus",
                ZipName = "NetStandardAzureServiceBus",
                TypeName = "ServiceControl.Transports.AzureServiceBusStandard.AzureServiceBusTransport, ServiceControl.Transports.AzureServiceBusStandard",
                SampleConnectionString = "Endpoint=sb://[namespace].servicebus.windows.net; SharedSecretIssuer=<owner>;SharedSecretValue=<someSecret>",
                Matches = name => name.Equals("AzureServiceBus .NET Standard", StringComparison.OrdinalIgnoreCase)
                          || name.Equals("ServiceControl.Transports.AzureServiceBusStandard.AzureServiceBusTransport, ServiceControl.Transports.AzureServiceBusStandard", StringComparison.OrdinalIgnoreCase)
            },
            new TransportInfo
            {
                Name = "AzureStorageQueue",
                ZipName = "AzureStorageQueue",
                TypeName = "ServiceControl.Transports.AzureStorageQueues.ServiceControlAzureStorageQueueTransport, ServiceControl.Transports.AzureStorageQueues",
                SampleConnectionString = "DefaultEndpointsProtocol=[http|https];AccountName=<MyAccountName>;AccountKey=<MyAccountKey>",
                Matches = name => name.Equals("AzureStorageQueue", StringComparison.OrdinalIgnoreCase)
                          || name.Equals("NServiceBus.AzureStorageQueueTransport, NServiceBus.Azure.Transports.WindowsAzureStorageQueues", StringComparison.OrdinalIgnoreCase)
                          || name.Equals("ServiceControl.Transports.AzureStorageQueues.ServiceControlAzureStorageQueueTransport, ServiceControl.Transports.AzureStorageQueues", StringComparison.OrdinalIgnoreCase)
            },
            new TransportInfo
            {
                Name = "MSMQ",
                ZipName = "Msmq",
                TypeName = "NServiceBus.MsmqTransport, NServiceBus.Transport.Msmq",
                SampleConnectionString = string.Empty,
                Default = true,
                Matches = name => name.Equals("MSMQ", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("NServiceBus.MsmqTransport, NServiceBus.Core", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("NServiceBus.MsmqTransport, NServiceBus.Transport.Msmq", StringComparison.OrdinalIgnoreCase)
            },
            new TransportInfo
            {
                Name = "SQLServer",
                ZipName = "SqlServer",
                TypeName = "ServiceControl.Transports.SQLServer.ServiceControlSQLServerTransport, ServiceControl.Transports.SQLServer",
                SampleConnectionString = "Data Source=<SQLInstance>;Initial Catalog=nservicebus;Integrated Security=True",
                Help = "When integrated authentication is specified in the SQL connection string the the current installing user is used to create the required SQL tables structure not the service account.",
                Matches = name => name.Equals("SQLServer", StringComparison.OrdinalIgnoreCase)
                          || name.Equals("ServiceControl.Transports.SQLServer.ServiceControlSQLServerTransport, ServiceControl.Transports.SQLServer", StringComparison.OrdinalIgnoreCase)
                          || name.Equals("NServiceBus.SqlServerTransport, NServiceBus.Transport.SQLServer", StringComparison.OrdinalIgnoreCase)
            },
            new TransportInfo
            {
                Name = "RabbitMQ - Conventional Routing Topology",
                ZipName = "RabbitMQ",
                TypeName = "ServiceControl.Transports.RabbitMQ.ConventialRoutingTopologyRabbitMQTransport, ServiceControl.Transports.RabbitMQ",
                SampleConnectionString = "host=<HOSTNAME>;username=<USERNAME>;password=<PASSWORD>",
                Matches = name => name.Equals("RabbitMQ", StringComparison.OrdinalIgnoreCase)
                          || name.Equals("RabbitMQ - Conventional Routing Topology", StringComparison.OrdinalIgnoreCase)
                          || name.Equals("NServiceBus.RabbitMQTransport, NServiceBus.Transports.RabbitMQ", StringComparison.OrdinalIgnoreCase)
                          || name.Equals("ServiceControl.Transports.RabbitMQ.ConventialRoutingTopologyRabbitMQTransport, ServiceControl.Transports.RabbitMQ", StringComparison.OrdinalIgnoreCase)
            },
            new TransportInfo
            {
                Name = "RabbitMQ - Direct Routing Topology",
                TypeName = "ServiceControl.Transports.RabbitMQ.DirectRoutingTopologyRabbitMQTransport, ServiceControl.Transports.RabbitMQ",
                ZipName = "RabbitMQ",
                SampleConnectionString = "host=<HOSTNAME>;username=<USERNAME>;password=<PASSWORD>",
                Matches = name => name.Equals("ServiceControl.Transports.RabbitMQ.DirectRoutingTopologyRabbitMQTransport, ServiceControl.Transports.RabbitMQ", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("RabbitMQ - Direct Routing Topology", StringComparison.OrdinalIgnoreCase)
            }
        };

        public static TransportInfo FindByName(string name)
        {
            return All.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
