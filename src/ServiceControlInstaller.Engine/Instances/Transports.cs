namespace ServiceControlInstaller.Engine.Instances
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public  class Transports
    {
        public static TransportInfo FindByName(string name)
        {
            return All.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        public static List<TransportInfo> All
        {
            get
            {
                // Between NSB v4 and v5 the transport type changes.
                // NSB v4 Transports had "Transport" added to the type
                // e.g type name for ASB was NServiceBus.AzureServiceBus in V4 and NServiceBus.AzureServiceBusTransport in V5
                // That why we have a MatchOn property

                return new List<TransportInfo>
                {
                    new TransportInfo
                    {
                        Name = "AzureServiceBus",
                        TypeName = "NServiceBus.AzureServiceBusTransport, NServiceBus.Azure.Transports.WindowsAzureServiceBus",
                        MatchOn = "NServiceBus.AzureServiceBus",
                        SampleConnectionString = @"Endpoint=sb://[namespace].servicebus.windows.net; SharedSecretIssuer=<owner>;SharedSecretValue=<someSecret>"

                    },
                    new TransportInfo
                    {
                        Name = "AzureStorageQueue",
                        TypeName = "NServiceBus.AzureStorageQueueTransport, NServiceBus.Azure.Transports.WindowsAzureStorageQueues",
                        MatchOn  = "NServiceBus.AzureStorageQueue",
                        SampleConnectionString = @"DefaultEndpointsProtocol=[http|https];AccountName=<MyAccountName>;AccountKey=<MyAccountKey>"
                    },
                    new TransportInfo
                    {
                        Name = "MSMQ",
                        TypeName = "NServiceBus.MsmqTransport, NServiceBus.Core", 
                        MatchOn = "NServiceBus.Msmq",
                        SampleConnectionString = @"",
                        Default = true
                    },
                    new TransportInfo
                    {
                        Name = "SQLServer",
                        TypeName = "NServiceBus.SqlServerTransport, NServiceBus.Transports.SQLServer",
                        MatchOn = "NServiceBus.SqlServer",
                        SampleConnectionString = @"Data Source=<SQLInstance>;Initial Catalog=nservicebus;Integrated Security=True"
                    },
                    new TransportInfo
                    {
                        Name = "RabbitMQ",
                        TypeName = "NServiceBus.RabbitMQTransport, NServiceBus.Transports.RabbitMQ",
                        MatchOn = "NServiceBus.RabbitMQ",
                        SampleConnectionString = @"host=<HOSTNAME>;username=<USERNAME>;password=<PASSWORD>"
                    }
                };
            }
        }
    }
}
