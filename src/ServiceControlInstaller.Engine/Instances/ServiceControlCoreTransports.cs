﻿namespace ServiceControlInstaller.Engine.Instances
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class ServiceControlCoreTransports
    {
        public static List<TransportInfo> All => new List<TransportInfo>
        {
            //INFO: Those types are used in the SCMU and in PS scripts. In both cases Match predicate is used to find a transport info.
            //      In the UI the matching is done based on the transport TypeName from app.config. In PS it's done based on human friendly names.
            //      As a result the Match predicate should evaluate to true both for TypeName and human friendly name.
            //      Matching separately on Name and TypeName would not be enough because we need to be backwards compatible.
            //      As a result Match is comparing to old/current names, old/current types.
            new TransportInfo
            {
                DisplayName = TransportNames.AmazonSQS,
                ZipName = "AmazonSQS",
                TypeName = "ServiceControl.Transports.SQS.SQSTransportCustomization, ServiceControl.Transports.SQS",
                SampleConnectionString = "Region=<REGION>;QueueNamePrefix=<prefix>;TopicNamePrefix=<prefix>;AccessKeyId=<ACCESSKEYID>;SecretAccessKey=<SECRETACCESSKEY>;S3BucketForLargeMessages=<BUCKETNAME>;S3KeyPrefix=<KEYPREFIX>",
                AvailableInSCMU = true,
                Help = "'Region' is mandatory. Specify 'AccessKeyId' and 'SecretAccessKey' values to set the AWS_ACCESS_KEY_ID/AWS_SECRET_ACCESS_KEY environment variables if not using IAM roles or EC2 metadata. Specify 'S3BucketForLargeMessages' and optionally 'S3KeyPrefix' if large message bodies are used.",
                Matches = name => name.Equals(TransportNames.AmazonSQS, StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("ServiceControl.Transports.SQS.SQSTransportCustomization, ServiceControl.Transports.SQS", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("ServiceControl.Transports.AmazonSQS.ServiceControlSqsTransport, ServiceControl.Transports.AmazonSQS", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("NServiceBus.SqsTransport, NServiceBus.AmazonSQS", StringComparison.OrdinalIgnoreCase)
            },
            new TransportInfo
            {
                DisplayName = TransportNames.AzureServiceBus,
                TypeName = "ServiceControl.Transports.ASBS.ASBSTransportCustomization, ServiceControl.Transports.ASBS",
                ZipName = "NetStandardAzureServiceBus",
                SampleConnectionString = "Endpoint=sb://[namespace].servicebus.windows.net; SharedSecretIssuer=<owner>;SharedSecretValue=<someSecret>;QueueLengthQueryDelayInterval=<IntervalInMilliseconds(Default=500ms)>;TopicName=<TopicBundleName(Default=bundle-1)>",
                AvailableInSCMU = true,
                Matches = name => name.Equals(TransportNames.AzureServiceBus, StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("ServiceControl.Transports.ASBS.ASBSTransportCustomization, ServiceControl.Transports.ASBS", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("ServiceControl.Transports.AzureServiceBus.AzureServiceBusTransport, ServiceControl.Transports.AzureServiceBus", StringComparison.OrdinalIgnoreCase)
            },
            new TransportInfo
            {
                DisplayName = TransportNames.AzureStorageQueue,
                TypeName = "ServiceControl.Transports.ASQ.ASQTransportCustomization, ServiceControl.Transports.ASQ",
                ZipName = "AzureStorageQueue",
                SampleConnectionString = "DefaultEndpointsProtocol=[http|https];AccountName=<MyAccountName>;AccountKey=<MyAccountKey>;Subscriptions Table=tablename",
                AvailableInSCMU = true,
                Help = "Specify optional 'Subscriptions Table' to override the default subscriptions table name.",
                Matches = name => name.Equals(TransportNames.AzureStorageQueue, StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("ServiceControl.Transports.ASQ.ASQTransportCustomization, ServiceControl.Transports.ASQ", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("NServiceBus.AzureStorageQueueTransport, NServiceBus.Azure.Transports.WindowsAzureStorageQueues", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("ServiceControl.Transports.AzureStorageQueues.ServiceControlAzureStorageQueueTransport, ServiceControl.Transports.AzureStorageQueues", StringComparison.OrdinalIgnoreCase)
            },
            new TransportInfo
            {
                DisplayName = TransportNames.MSMQ,
                TypeName = "ServiceControl.Transports.Msmq.MsmqTransportCustomization, ServiceControl.Transports.Msmq",
                ZipName = "MSMQ",
                SampleConnectionString = string.Empty,
                Default = true,
                AvailableInSCMU = true,
                Matches = name => name.Equals(TransportNames.MSMQ, StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("ServiceControl.Transports.Msmq.MsmqTransportCustomization, ServiceControl.Transports.Msmq", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("NServiceBus.MsmqTransport, NServiceBus.Core", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("NServiceBus.MsmqTransport, NServiceBus.Transport.Msmq", StringComparison.OrdinalIgnoreCase)
            },
            new TransportInfo
            {
                DisplayName = TransportNames.RabbitMQConventionalRoutingTopologyDeprecated,
                TypeName = "ServiceControl.Transports.RabbitMQ.RabbitMQConventionalRoutingTransportCustomization, ServiceControl.Transports.RabbitMQ",
                ZipName = "RabbitMQ",
                SampleConnectionString = "host=<HOSTNAME>;username=<USERNAME>;password=<PASSWORD>;DisableRemoteCertificateValidation=<true|false(default)>;UseExternalAuthMechanism=<true|false(default)>",
                AvailableInSCMU = false,
                AutoMigrateTo = TransportNames.RabbitMQClassicConventionalRoutingTopology,
                Matches = name => name.Equals(TransportNames.RabbitMQConventionalRoutingTopologyDeprecated, StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("RabbitMQ", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("ServiceControl.Transports.RabbitMQ.RabbitMQConventionalRoutingTransportCustomization, ServiceControl.Transports.RabbitMQ", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("NServiceBus.RabbitMQTransport, NServiceBus.Transports.RabbitMQ", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("ServiceControl.Transports.RabbitMQ.ConventialRoutingTopologyRabbitMQTransport, ServiceControl.Transports.RabbitMQ", StringComparison.OrdinalIgnoreCase)
            },
            new TransportInfo
            {
                DisplayName = TransportNames.RabbitMQClassicConventionalRoutingTopology,
                TypeName = "ServiceControl.Transports.RabbitMQ.RabbitMQClassicConventionalRoutingTransportCustomization, ServiceControl.Transports.RabbitMQ",
                ZipName = "RabbitMQ",
                SampleConnectionString = "host=<HOSTNAME>;username=<USERNAME>;password=<PASSWORD>;DisableRemoteCertificateValidation=<true|false(default)>;UseExternalAuthMechanism=<true|false(default)>",
                AvailableInSCMU = true,
                Matches = name => name.Equals(TransportNames.RabbitMQClassicConventionalRoutingTopology, StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("RabbitMQ", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("ServiceControl.Transports.RabbitMQ.RabbitMQClassicConventionalRoutingTransportCustomization, ServiceControl.Transports.RabbitMQ", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("NServiceBus.RabbitMQTransport, NServiceBus.Transports.RabbitMQ", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("ServiceControl.Transports.RabbitMQ.ClassicConventialRoutingTopologyRabbitMQTransport, ServiceControl.Transports.RabbitMQ", StringComparison.OrdinalIgnoreCase)
            },
            new TransportInfo
            {
                DisplayName = TransportNames.RabbitMQQuorumConventionalRoutingTopology,
                TypeName = "ServiceControl.Transports.RabbitMQ.RabbitMQQuorumConventionalRoutingTransportCustomization, ServiceControl.Transports.RabbitMQ",
                ZipName = "RabbitMQ",
                SampleConnectionString = "host=<HOSTNAME>;username=<USERNAME>;password=<PASSWORD>;DisableRemoteCertificateValidation=<true|false(default)>;UseExternalAuthMechanism=<true|false(default)>",
                AvailableInSCMU = true,
                Matches = name => name.Equals(TransportNames.RabbitMQQuorumConventionalRoutingTopology, StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("RabbitMQ", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("ServiceControl.Transports.RabbitMQ.RabbitMQQuorumConventionalRoutingTransportCustomization, ServiceControl.Transports.RabbitMQ", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("NServiceBus.RabbitMQTransport, NServiceBus.Transports.RabbitMQ", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("ServiceControl.Transports.RabbitMQ.QuorumConventialRoutingTopologyRabbitMQTransport, ServiceControl.Transports.RabbitMQ", StringComparison.OrdinalIgnoreCase)
            },
            new TransportInfo
            {
                DisplayName = TransportNames.RabbitMQDirectRoutingTopologyDeprecated,
                TypeName = "ServiceControl.Transports.RabbitMQ.RabbitMQDirectRoutingTransportCustomization, ServiceControl.Transports.RabbitMQ",
                ZipName = "RabbitMQ",
                SampleConnectionString = "host=<HOSTNAME>;username=<USERNAME>;password=<PASSWORD>;DisableRemoteCertificateValidation=<true|false(default)>;UseExternalAuthMechanism=<true|false(default)>",
                AvailableInSCMU = false,
                AutoMigrateTo = TransportNames.RabbitMQClassicDirectRoutingTopology,
                Matches = name => name.Equals(TransportNames.RabbitMQDirectRoutingTopologyDeprecated, StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("ServiceControl.Transports.RabbitMQ.RabbitMQDirectRoutingTransportCustomization, ServiceControl.Transports.RabbitMQ", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("ServiceControl.Transports.RabbitMQ.DirectRoutingTopologyRabbitMQTransport, ServiceControl.Transports.RabbitMQ", StringComparison.OrdinalIgnoreCase)
            },
            new TransportInfo
            {
                DisplayName = TransportNames.RabbitMQClassicDirectRoutingTopology,
                TypeName = "ServiceControl.Transports.RabbitMQ.RabbitMQClassicDirectRoutingTransportCustomization, ServiceControl.Transports.RabbitMQ",
                ZipName = "RabbitMQ",
                SampleConnectionString = "host=<HOSTNAME>;username=<USERNAME>;password=<PASSWORD>;DisableRemoteCertificateValidation=<true|false(default)>;UseExternalAuthMechanism=<true|false(default)>",
                AvailableInSCMU = true,
                Matches = name => name.Equals(TransportNames.RabbitMQClassicDirectRoutingTopology, StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("ServiceControl.Transports.RabbitMQ.RabbitMQClassicDirectRoutingTransportCustomization, ServiceControl.Transports.RabbitMQ", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("ServiceControl.Transports.RabbitMQ.ClassicDirectRoutingTopologyRabbitMQTransport, ServiceControl.Transports.RabbitMQ", StringComparison.OrdinalIgnoreCase)
            },
            new TransportInfo
            {
                DisplayName = TransportNames.RabbitMQQuorumDirectRoutingTopology,
                TypeName = "ServiceControl.Transports.RabbitMQ.RabbitMQQuorumDirectRoutingTransportCustomization, ServiceControl.Transports.RabbitMQ",
                ZipName = "RabbitMQ",
                SampleConnectionString = "host=<HOSTNAME>;username=<USERNAME>;password=<PASSWORD>;DisableRemoteCertificateValidation=<true|false(default)>;UseExternalAuthMechanism=<true|false(default)>",
                AvailableInSCMU = true,
                Matches = name => name.Equals(TransportNames.RabbitMQQuorumDirectRoutingTopology, StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("ServiceControl.Transports.RabbitMQ.RabbitMQQuorumDirectRoutingTransportCustomization, ServiceControl.Transports.RabbitMQ", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("ServiceControl.Transports.RabbitMQ.QuorumDirectRoutingTopologyRabbitMQTransport, ServiceControl.Transports.RabbitMQ", StringComparison.OrdinalIgnoreCase)
            },
            new TransportInfo
            {
                DisplayName = TransportNames.SQLServer,
                TypeName = "ServiceControl.Transports.SqlServer.SqlServerTransportCustomization, ServiceControl.Transports.SqlServer",
                ZipName = "SQLServer",
                SampleConnectionString = "Data Source=<SQLInstance>;Initial Catalog=nservicebus;Integrated Security=True;Queue Schema=myschema;Subscriptions Table=tablename@schema@catalog",
                AvailableInSCMU = true,
                Help = "Specify optional 'Queue Schema' to override the default schema. Specify optional 'Subscriptions Table' to override the default subscriptions table location. When integrated authentication is specified in the SQL connection string the the current installing user is used to create the required SQL tables structure not the service account.",
                Matches = name => name.Equals(TransportNames.SQLServer, StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("ServiceControl.Transports.SqlServer.SqlServerTransportCustomization, ServiceControl.Transports.SqlServer", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("NServiceBus.SqlServerTransport, NServiceBus.Transports.SQLServer", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("ServiceControl.Transports.SQLServer.ServiceControlSQLServerTransport, ServiceControl.Transports.SQLServer", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("NServiceBus.SqlServerTransport, NServiceBus.Transport.SQLServer", StringComparison.OrdinalIgnoreCase)
            },
            new TransportInfo
            {
                DisplayName = TransportNames.AzureServiceBusEndpointOrientedTopologyDeprecated,
                TypeName = "ServiceControl.Transports.ASB.ASBEndpointTopologyTransportCustomization, ServiceControl.Transports.ASB",
                ZipName = "AzureServiceBus",
                SampleConnectionString = "Endpoint=sb://[namespace].servicebus.windows.net; SharedSecretIssuer=<owner>;SharedSecretValue=<someSecret>;QueueLengthQueryDelayInterval=<IntervalInMilliseconds(Default=500ms)>",
                AvailableInSCMU = false,
                Matches = name => name.Equals(TransportNames.AzureServiceBusEndpointOrientedTopologyDeprecated, StringComparison.OrdinalIgnoreCase)
                                  || name.Equals(TransportNames.AzureServiceBusEndpointOrientedTopologyLegacy, StringComparison.OrdinalIgnoreCase)
                                  || name.Equals(TransportNames.AzureServiceBusEndpointOrientedTopologyOld, StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("AzureServiceBus", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("ServiceControl.Transports.ASB.ASBEndpointTopologyTransportCustomization, ServiceControl.Transports.ASB", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("NServiceBus.AzureServiceBusTransport, NServiceBus.Azure.Transports.WindowsAzureServiceBus", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("ServiceControl.Transports.LegacyAzureServiceBus.EndpointOrientedTopologyAzureServiceBusTransport, ServiceControl.Transports.LegacyAzureServiceBus", StringComparison.OrdinalIgnoreCase)
            },
            new TransportInfo
            {
                DisplayName = TransportNames.AzureServiceBusForwardingTopologyDeprecated,
                TypeName = "ServiceControl.Transports.ASB.ASBForwardingTopologyTransportCustomization, ServiceControl.Transports.ASB",
                ZipName = "AzureServiceBus",
                SampleConnectionString = "Endpoint=sb://[namespace].servicebus.windows.net; SharedSecretIssuer=<owner>;SharedSecretValue=<someSecret>;QueueLengthQueryDelayInterval=<IntervalInMilliseconds(Default=500ms)>",
                AvailableInSCMU = false,
                ////Legacy ASB Forwarding Topology should be migrated to the ASBS transport seam
                AutoMigrateTo = TransportNames.AzureServiceBus,
                Matches = name => name.Equals(TransportNames.AzureServiceBusForwardingTopologyDeprecated, StringComparison.OrdinalIgnoreCase)
                                  || name.Equals(TransportNames.AzureServiceBusForwardingTopologyLegacy, StringComparison.OrdinalIgnoreCase)
                                  || name.Equals(TransportNames.AzureServiceBusForwardingTopologyOld, StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("ServiceControl.Transports.ASB.ASBForwardingTopologyTransportCustomization, ServiceControl.Transports.ASB", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("AzureServiceBus", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("NServiceBus.AzureServiceBusTransport, NServiceBus.Azure.Transports.WindowsAzureServiceBus", StringComparison.OrdinalIgnoreCase)
                                  || name.Equals("ServiceControl.Transports.LegacyAzureServiceBus.ForwardingTopologyAzureServiceBusTransport, ServiceControl.Transports.LegacyAzureServiceBus", StringComparison.OrdinalIgnoreCase)
            },
            new TransportInfo
            {
                DisplayName = TransportNames.LearningTransport,
                TypeName = "ServiceControl.Transports.Learning.LearningTransportCustomization, ServiceControl.Transports.Learning",
                ZipName = "LearningTransport",
                SampleConnectionString = "%TEMP%\\.learningtransport",
                AvailableInSCMU = IncludeLearningTransport(),
                Matches = name => name.Equals(TransportNames.LearningTransport, StringComparison.OrdinalIgnoreCase)
                || name.Equals("ServiceControl.Transports.Learning.LearningTransportCustomization, ServiceControl.Transports.Learning", StringComparison.OrdinalIgnoreCase)
            },
        };

        static bool IncludeLearningTransport()
        {
            try
            {
                var environmentValue = Environment.GetEnvironmentVariable("ServiceControl_IncludeLearningTransport");

                if (environmentValue != null)
                {
                    environmentValue = Environment.ExpandEnvironmentVariables(environmentValue);
                    if (bool.TryParse(environmentValue, out var enabled))
                    {
                        return enabled;
                    }
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }


        public static TransportInfo Find(string name)
        {
            return All.FirstOrDefault(p => p.Matches(name));
        }

        public static TransportInfo UpgradedTransportSeam(TransportInfo transport)
        {
            if (!string.IsNullOrWhiteSpace(transport.AutoMigrateTo))
            {
                return Find(transport.AutoMigrateTo);
            }

            return transport;
        }

    }
}
