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
                return new List<TransportInfo>
                {
                    new TransportInfo
                    {
                        Name = "AzureServiceBus",
                        TypeName = "NServiceBus.AzureServiceBus, NServiceBus.Azure.Transports.WindowsAzureServiceBus",
                        SampleConnectionString = @"Endpoint=sb://[namespace].servicebus.windows.net; SharedSecretIssuer=<owner>;SharedSecretValue=<someSecret>"

                    },
                    new TransportInfo
                    {
                        Name = "AzureStorageQueue",
                        TypeName = "NServiceBus.AzureStorageQueue, NServiceBus.Azure.Transports.WindowsAzureStorageQueues",
                        SampleConnectionString = @"DefaultEndpointsProtocol=[http|https];AccountName=<MyAccountName>;AccountKey=<MyAccountKey>"
                    },
                    new TransportInfo
                    {
                        Name = "MSMQ",
                        TypeName = "NServiceBus.Msmq, NServiceBus.Core",
                        SampleConnectionString = @"",
                        Default = true
                    },
                    new TransportInfo
                    {
                        Name = "SQLServer",
                        TypeName = "NServiceBus.SqlServer, NServiceBus.Transports.SQLServer",
                        SampleConnectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True"
                    },
                    new TransportInfo
                    {
                        Name = "RabbitMQ",
                        TypeName = "NServiceBus.RabbitMQ, NServiceBus.Transports.RabbitMQ",
                        SampleConnectionString = @"host=localhost;username=<USERNAME>;password=<PASSWORD>"
                    }
                };
            }
        }
    }
}
