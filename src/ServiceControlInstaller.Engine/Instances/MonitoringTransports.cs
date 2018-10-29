namespace ServiceControlInstaller.Engine.Instances
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class MonitoringTransports
    {
        public static List<TransportInfo> All => new List<TransportInfo>
        {
            //INFO: Those types are used in the SCMU and in PS scripts. In both cases Match predicate is used to find a transport info.
            //      In the UI the matching is done based on the transport TypeName from app.config. In PS it's done based on human friendly names.
            //      As a result the Match predicate should evalute to true both for TypeName and human friendly name.
            //      Matching separately on Name and TypeName would not be enough because we need to be backwards compatible.
            //      As a result Match is comparing to old/current names, old/current types.
            new TransportInfo
            {
                Name = TransportNames.AmazonSQS,
                ZipName = "AmazonSQS",
                TypeName = "ServiceControl.Transports.AmazonSQS.ServiceControlSqsTransport, ServiceControl.Transports.AmazonSQS",
                SampleConnectionString = "AccessKeyId=<ACCESSKEYID>;SecretAccessKey=<SECRETACCESSKEY>;Region=<REGION>;QueueNamePrefix=<prefix>",
                Help = "'AccessKeyId', 'SecretAccessKey' and 'Region' are mandatory configurations.",
                Matches = name => name.Equals(TransportNames.AmazonSQS, StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("ServiceControl.Transports.AmazonSQS.ServiceControlSqsTransport, ServiceControl.Transports.AmazonSQS", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("NServiceBus.SqsTransport, NServiceBus.AmazonSQS", StringComparison.OrdinalIgnoreCase)
            },
            new TransportInfo
            {
                Name = TransportNames.AzureServiceBusEndpointOrientedTopology,
                ZipName = "LegacyAzureServiceBus",
                TypeName = "ServiceControl.Transports.LegacyAzureServiceBus.EndpointOrientedTopologyAzureServiceBusTransport, ServiceControl.Transports.LegacyAzureServiceBus",
                SampleConnectionString = "Endpoint=sb://[namespace].servicebus.windows.net; SharedSecretIssuer=<owner>;SharedSecretValue=<someSecret>",
                Matches = name => name.Equals(TransportNames.AzureServiceBusEndpointOrientedTopology, StringComparison.OrdinalIgnoreCase)
                          || name.Equals("ServiceControl.Transports.LegacyAzureServiceBus.EndpointOrientedTopologyAzureServiceBusTransport, ServiceControl.Transports.LegacyAzureServiceBus", StringComparison.OrdinalIgnoreCase)
            },
            new TransportInfo
            {
                Name = TransportNames.AzureServiceBusForwardingTopology,
                ZipName = "LegacyAzureServiceBus",
                TypeName = "ServiceControl.Transports.LegacyAzureServiceBus.ForwardingTopologyAzureServiceBusTransport, ServiceControl.Transports.LegacyAzureServiceBus",
                SampleConnectionString = "Endpoint=sb://[namespace].servicebus.windows.net; SharedSecretIssuer=<owner>;SharedSecretValue=<someSecret>",
                Matches = name => name.Equals(TransportNames.AzureServiceBusForwardingTopology, StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("AzureServiceBus", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("NServiceBus.AzureServiceBusTransport, NServiceBus.Azure.Transports.WindowsAzureServiceBus", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("ServiceControl.Transports.LegacyAzureServiceBus.ForwardingTopologyAzureServiceBusTransport, ServiceControl.Transports.LegacyAzureServiceBus", StringComparison.OrdinalIgnoreCase)
            },
            new TransportInfo
            {
                Name = TransportNames.AzureServiceBus,
                ZipName = "AzureServiceBus",
                TypeName = "ServiceControl.Transports.AzureServiceBus.AzureServiceBusTransport, ServiceControl.Transports.AzureServiceBus",
                SampleConnectionString = "Endpoint=sb://[namespace].servicebus.windows.net; SharedSecretIssuer=<owner>;SharedSecretValue=<someSecret>",
                Matches = name => name.Equals(TransportNames.AzureServiceBus, StringComparison.OrdinalIgnoreCase)
                          || name.Equals("ServiceControl.Transports.AzureServiceBus.AzureServiceBusTransport, ServiceControl.Transports.AzureServiceBus", StringComparison.OrdinalIgnoreCase)
            },
            new TransportInfo
            {
                Name = TransportNames.AzureStorageQueue,
                ZipName = "AzureStorageQueue",
                TypeName = "ServiceControl.Transports.AzureStorageQueues.ServiceControlAzureStorageQueueTransport, ServiceControl.Transports.AzureStorageQueues",
                SampleConnectionString = "DefaultEndpointsProtocol=[http|https];AccountName=<MyAccountName>;AccountKey=<MyAccountKey>",
                Matches = name => name.Equals(TransportNames.AzureStorageQueue, StringComparison.OrdinalIgnoreCase)
                          || name.Equals("NServiceBus.AzureStorageQueueTransport, NServiceBus.Azure.Transports.WindowsAzureStorageQueues", StringComparison.OrdinalIgnoreCase)
                          || name.Equals("ServiceControl.Transports.AzureStorageQueues.ServiceControlAzureStorageQueueTransport, ServiceControl.Transports.AzureStorageQueues", StringComparison.OrdinalIgnoreCase)
            },
            new TransportInfo
            {
                Name = TransportNames.MSMQ,
                ZipName = "Msmq",
                TypeName = "NServiceBus.MsmqTransport, NServiceBus.Transport.Msmq",
                SampleConnectionString = string.Empty,
                Default = true,
                Matches = name => name.Equals(TransportNames.MSMQ, StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("NServiceBus.MsmqTransport, NServiceBus.Core", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("NServiceBus.MsmqTransport, NServiceBus.Transport.Msmq", StringComparison.OrdinalIgnoreCase)
            },
            new TransportInfo
            {
                Name = TransportNames.SQLServer,
                ZipName = "SqlServer",
                TypeName = "ServiceControl.Transports.SQLServer.ServiceControlSQLServerTransport, ServiceControl.Transports.SQLServer",
                SampleConnectionString = "Data Source=<SQLInstance>;Initial Catalog=nservicebus;Integrated Security=True",
                Help = "When integrated authentication is specified in the SQL connection string the the current installing user is used to create the required SQL tables structure not the service account.",
                Matches = name => name.Equals(TransportNames.SQLServer, StringComparison.OrdinalIgnoreCase)
                          || name.Equals("ServiceControl.Transports.SQLServer.ServiceControlSQLServerTransport, ServiceControl.Transports.SQLServer", StringComparison.OrdinalIgnoreCase)
                          || name.Equals("NServiceBus.SqlServerTransport, NServiceBus.Transport.SQLServer", StringComparison.OrdinalIgnoreCase)
            },
            new TransportInfo
            {
                Name = TransportNames.RabbitMQConventionalRoutingTopology,
                ZipName = "RabbitMQ",
                TypeName = "ServiceControl.Transports.RabbitMQ.ConventialRoutingTopologyRabbitMQTransport, ServiceControl.Transports.RabbitMQ",
                SampleConnectionString = "host=<HOSTNAME>;username=<USERNAME>;password=<PASSWORD>",
                Matches = name => name.Equals("RabbitMQ", StringComparison.OrdinalIgnoreCase)
                          || name.Equals(TransportNames.RabbitMQConventionalRoutingTopology, StringComparison.OrdinalIgnoreCase)
                          || name.Equals("NServiceBus.RabbitMQTransport, NServiceBus.Transports.RabbitMQ", StringComparison.OrdinalIgnoreCase)
                          || name.Equals("ServiceControl.Transports.RabbitMQ.ConventialRoutingTopologyRabbitMQTransport, ServiceControl.Transports.RabbitMQ", StringComparison.OrdinalIgnoreCase)
            },
            new TransportInfo
            {
                Name = TransportNames.RabbitMQDriectRoutingTopology,
                TypeName = "ServiceControl.Transports.RabbitMQ.DirectRoutingTopologyRabbitMQTransport, ServiceControl.Transports.RabbitMQ",
                ZipName = "RabbitMQ",
                SampleConnectionString = "host=<HOSTNAME>;username=<USERNAME>;password=<PASSWORD>",
                Matches = name => name.Equals("ServiceControl.Transports.RabbitMQ.DirectRoutingTopologyRabbitMQTransport, ServiceControl.Transports.RabbitMQ", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals(TransportNames.RabbitMQDriectRoutingTopology, StringComparison.OrdinalIgnoreCase)
            }
        };

        public static TransportInfo FindByName(string name)
        {
            return All.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
