namespace ServiceControl.EndpointPlugin.Operations.ServiceControlBackend
{
    using System;
    using System.Configuration;
    using System.IO;
    using Messages.CustomChecks;
    using Messages.Heartbeats;
    using Messages.Operations.ServiceControlBackend;
    using NServiceBus;
    using NServiceBus.MessageInterfaces.MessageMapper.Reflection;
    using NServiceBus.ObjectBuilder;
    using NServiceBus.Serializers.Json;
    using NServiceBus.Transports;
    using NServiceBus.Unicast;

    public class ServiceControlBackend : IServiceControlBackend
    {
        public ISendMessages MessageSender { get; set; }
        public IBuilder Builder { get; set; }
        MessageMapper messageMapper;
        JsonMessageSerializer serializer;
        Address serviceControlBackendAddress;

        public ServiceControlBackend()
        {
            messageMapper = new MessageMapper();
            // TODO: Should we initialize with all known types of ServiceControl messages here??
            messageMapper.Initialize(new[] { typeof(EndpointHeartbeat), typeof(ReportCustomCheckResult) });

            serializer = new JsonMessageSerializer(messageMapper);

            // Initialize the backend address
            serviceControlBackendAddress = GetServiceControlAddress();
        }

        public void Send(object messageToSend, TimeSpan timeToBeReceived)
        {
            var message = new TransportMessage();
            message.TimeToBeReceived = timeToBeReceived;

            using (var stream = new MemoryStream())
            {
                serializer.Serialize(new object[] { messageToSend }, stream);
                message.Body = stream.ToArray();
            }

            MessageSender.Send(message, serviceControlBackendAddress);
        }

        public void Send(object messageToSend)
        {
            var message = new TransportMessage();
            using (var stream = new MemoryStream())
            {
                serializer.Serialize(new object[] { messageToSend }, stream);
                message.Body = stream.ToArray();
            }
            MessageSender.Send(message, serviceControlBackendAddress);
        }

        Address GetServiceControlAddress()
        {
            var queueName = ConfigurationManager.AppSettings[@"ServiceControl/Queue"];
            if (!String.IsNullOrEmpty(queueName))
            {
                return Address.Parse(queueName);
            }

            var unicastBus = Configure.Instance.Builder.Build<UnicastBus>();
            var forwardAddress = unicastBus.ForwardReceivedMessagesTo;
            if (forwardAddress != null)
            {
                return new Address("Particular.ServiceControl", forwardAddress.Machine);
            }

            var errorAddress = ConfigureFaultsForwarder.ErrorQueue;
            if (errorAddress != null)
            {
                return new Address("Particular.ServiceControl", errorAddress.Machine);
            }

            return null;
        }
    }
}
