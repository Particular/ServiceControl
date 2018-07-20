namespace ServiceControlInstaller.Engine.Instances
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public  class ServiceControlCoreTransports
    {
        public static TransportInfo Find(string name)
        {
            return All.FirstOrDefault(p => p.Matches(name));
        }

        public static List<TransportInfo> All => new List<TransportInfo>
        {
            new TransportInfo
            {
                Name = "AzureServiceBus",
                TypeName = "ServiceControl.Transports.ASB.ASBEndpointTopologyTransportCustomization, ServiceControl.Transports.ASB",
                ZipName = "AzureServiceBus",
                SampleConnectionString = "Endpoint=sb://[namespace].servicebus.windows.net; SharedSecretIssuer=<owner>;SharedSecretValue=<someSecret>",
                Matches = name => name.Equals("AzureServiceBus", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("ServiceControl.Transports.ASB.ASBEndpointTopologyTransportCustomization, ServiceControl.Transports.ASB", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("NServiceBus.AzureServiceBusTransport, NServiceBus.Azure.Transports.WindowsAzureServiceBus", StringComparison.OrdinalIgnoreCase)
            },
            new TransportInfo
            {
                Name = "AzureStorageQueue",
                TypeName = "ServiceControl.Transports.ASQ.ASQTransportCustomization, ServiceControl.Transports.ASQ",
                ZipName = "AzureStorageQueue",
                SampleConnectionString = "DefaultEndpointsProtocol=[http|https];AccountName=<MyAccountName>;AccountKey=<MyAccountKey>",
                Matches = name => name.Equals("AzureStorageQueue", StringComparison.OrdinalIgnoreCase)
                                  ||name.Equals("ServiceControl.Transports.ASQ.ASQTransportCustomization, ServiceControl.Transports.ASQ", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("NServiceBus.AzureStorageQueueTransport, NServiceBus.Azure.Transports.WindowsAzureStorageQueues", StringComparison.OrdinalIgnoreCase)
            },
            new TransportInfo
            {
                Name = "MSMQ",
                TypeName = "ServiceControl.Transports.Msmq.MsmqTransportCustomization, ServiceControl.Transports.Msmq",
                ZipName = "Msmq",
                SampleConnectionString = string.Empty,
                Default = true,
                Matches = name => name.Equals("MSMQ", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("ServiceControl.Transports.Msmq.MsmqTransportCustomization, ServiceControl.Transports.Msmq", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("NServiceBus.MsmqTransport, NServiceBus.Core", StringComparison.OrdinalIgnoreCase)
            },
            new TransportInfo
            {
                Name = "SQLServer",
                TypeName = "ServiceControl.Transports.SqlServer.SqlServerTransportCustomization, ServiceControl.Transports.SqlServer",
                ZipName = "SqlServer",
                SampleConnectionString = "Data Source=<SQLInstance>;Initial Catalog=nservicebus;Integrated Security=True",
                Help = "When integrated authentication is specified in the SQL connection string the the current installing user is used to create the required SQL tables structure not the service account.",
                Matches = name => name.Equals("SQLServer", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("ServiceControl.Transports.SqlServer.SqlServerTransportCustomization, ServiceControl.Transports.SqlServer", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("NServiceBus.SqlServerTransport, NServiceBus.Transports.SQLServer", StringComparison.OrdinalIgnoreCase)
            },
            new TransportInfo
            {
                Name = "RabbitMQ",
                TypeName = "ServiceControl.Transports.RabbitMQ.RabbitMQConventionalRoutingTransportCustomization, ServiceControl.Transports.RabbitMQ",
                ZipName = "RabbitMQ",
                SampleConnectionString = "host=<HOSTNAME>;username=<USERNAME>;password=<PASSWORD>",
                Matches = name => name.Equals("RabbitMQ", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("ServiceControl.Transports.RabbitMQ.RabbitMQConventionalRoutingTransportCustomization, ServiceControl.Transports.RabbitMQ", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("NServiceBus.RabbitMQTransport, NServiceBus.Transports.RabbitMQ", StringComparison.OrdinalIgnoreCase)
            }
        };
    }
}