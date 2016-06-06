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

        public static List<TransportInfo> All => new List<TransportInfo>
        {
            new TransportInfo
            {
                Name = "AzureServiceBus",
                TypeName = "NServiceBus.AzureServiceBusTransport, NServiceBus.Azure.Transports.WindowsAzureServiceBus",
                MatchOn = "NServiceBus.AzureServiceBus",
                SampleConnectionString = "Endpoint=sb://[namespace].servicebus.windows.net; SharedSecretIssuer=<owner>;SharedSecretValue=<someSecret>"

            },
            new TransportInfo
            {
                Name = "AzureStorageQueue",
                TypeName = "NServiceBus.AzureStorageQueueTransport, NServiceBus.Azure.Transports.WindowsAzureStorageQueues",
                MatchOn  = "NServiceBus.AzureStorageQueue",
                SampleConnectionString = "DefaultEndpointsProtocol=[http|https];AccountName=<MyAccountName>;AccountKey=<MyAccountKey>"
            },
            new TransportInfo
            {
                Name = "MSMQ",
                TypeName = "NServiceBus.MsmqTransport, NServiceBus.Core", 
                MatchOn = "NServiceBus.Msmq",
                SampleConnectionString = String.Empty,
                Default = true
            },
            new TransportInfo
            {
                Name = "SQLServer",
                TypeName = "NServiceBus.SqlServerTransport, NServiceBus.Transports.SQLServer",
                MatchOn = "NServiceBus.SqlServer",
                SampleConnectionString = "Data Source=<SQLInstance>;Initial Catalog=nservicebus;Integrated Security=True"
            },
            new TransportInfo
            {
                Name = "SQLServer with multi-catalog",
                TypeName = "ServiceControl.Transports.SqlServerWithDTC.SqlServerWithDTCTransport, ServiceControl.Transports.SqlServerWithDTC",
                MatchOn = "ServiceControl.Transports.SqlServerWithDTC.SqlServerWithDTCTransport",
                SampleConnectionString = "Data Source=<SQLInstance>;Initial Catalog=nservicebus;Integrated Security=True",
                Warning = "Only use this transport selection if you are using multi-catalog option in SqlServer endpoints."
            },
            new TransportInfo
            {
                Name = "RabbitMQ",
                TypeName = "NServiceBus.RabbitMQTransport, NServiceBus.Transports.RabbitMQ",
                MatchOn = "NServiceBus.RabbitMQ",
                SampleConnectionString = "host=<HOSTNAME>;username=<USERNAME>;password=<PASSWORD>"
            }
        };
    }
}
