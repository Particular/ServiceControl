﻿using System;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Transport;
using RabbitMQ.Client;
using ServiceBus.Management.AcceptanceTests;
using ServiceControl.Transports.RabbitMQ;

public class ConfigureEndpointRabbitMQDirectRoutingTransport : ITransportIntegration
{
    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
    {
        connectionStringBuilder = new DbConnectionStringBuilder {ConnectionString = ConnectionString};

        var transport = configuration.UseTransport<RabbitMQTransport>();
        transport.UseDirectRoutingTopology();
        transport.ConnectionString(connectionStringBuilder.ConnectionString);

        queueBindings = configuration.GetSettings().Get<QueueBindings>();

        return Task.FromResult(0);
    }

    public Task Cleanup()
    {
        PurgeQueues();

        return Task.FromResult(0);
    }

    public string Name => "RabbitMQ - Direct Routing Topology";
    public string TypeName => $"{typeof(RabbitMQDirectRoutingTransportCustomization).AssemblyQualifiedName}";
    public string ConnectionString { get; set; }

    void PurgeQueues()
    {
        if (connectionStringBuilder == null)
        {
            return;
        }

        var connectionFactory = new ConnectionFactory
        {
            AutomaticRecoveryEnabled = true,
            UseBackgroundThreadsForIO = true
        };

        if (connectionStringBuilder.TryGetValue("username", out var value))
        {
            connectionFactory.UserName = value.ToString();
        }

        if (connectionStringBuilder.TryGetValue("password", out value))
        {
            connectionFactory.Password = value.ToString();
        }

        if (connectionStringBuilder.TryGetValue("virtualhost", out value))
        {
            connectionFactory.VirtualHost = value.ToString();
        }

        if (connectionStringBuilder.TryGetValue("host", out value))
        {
            connectionFactory.HostName = value.ToString();
        }
        else
        {
            throw new Exception("The connection string doesn't contain a value for 'host'.");
        }

        var queues = queueBindings.ReceivingAddresses.Concat(queueBindings.SendingAddresses);

        using (var connection = connectionFactory.CreateConnection("Test Queue Purger"))
        using (var channel = connection.CreateModel())
        {
            foreach (var queue in queues)
            {
                try
                {
                    channel.QueuePurge(queue);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Unable to clear queue {0}: {1}", queue, ex);
                }
            }
        }
    }

    DbConnectionStringBuilder connectionStringBuilder;
    QueueBindings queueBindings;
}