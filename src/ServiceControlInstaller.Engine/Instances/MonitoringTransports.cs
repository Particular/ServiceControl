namespace ServiceControlInstaller.Engine.Instances
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class MonitoringTransports
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
                SampleConnectionString = "Endpoint=sb://[namespace].servicebus.windows.net; SharedSecretIssuer=<owner>;SharedSecretValue=<someSecret>"

            },
            new TransportInfo
            {
                Name = "AzureStorageQueue",
                TypeName = "NServiceBus.AzureStorageQueueTransport, NServiceBus.Azure.Transports.WindowsAzureStorageQueues",
                SampleConnectionString = "DefaultEndpointsProtocol=[http|https];AccountName=<MyAccountName>;AccountKey=<MyAccountKey>"
            },
            new TransportInfo
            {
                Name = "MSMQ",
                TypeName = "NServiceBus.MsmqTransport, NServiceBus.Core",
                SampleConnectionString = string.Empty,
                Default = true
            },
            new TransportInfo
            {
                Name = "SQLServer",
                TypeName = "NServiceBus.SqlServerTransport, NServiceBus.Transport.SQLServer",
                SampleConnectionString = "Data Source=<SQLInstance>;Initial Catalog=nservicebus;Integrated Security=True",
                Help = "When integrated authentication is specified in the SQL connection string the the current installing user is used to create the required SQL tables structure not the service account."
            },
            new TransportInfo
            {
                Name = "RabbitMQ",
                TypeName = "NServiceBus.RabbitMQTransport, NServiceBus.Transports.RabbitMQ",
                SampleConnectionString = "host=<HOSTNAME>;username=<USERNAME>;password=<PASSWORD>"
            },
            new TransportInfo
            {
                Name = "AmazonSQS",
                TypeName = "NServiceBus.SqsTransport, NServiceBus.AmazonSQS",
                SampleConnectionString = "AccessKeyId=<ACCESSKEYID>;SecretAccessKey=<SECRETACCESSKEY>;Region=<REGION>",
                Help = "AccessKeyId will be promoted to AWS_ACCESS_KEY_ID, SecretAccessKey to AWS_SECRET_ACCESS_KEY and Region to AWS_REGION environment variable."
            }
        };
    }
}