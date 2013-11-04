namespace ServiceControl.EndpointPlugin.Operations.ServiceControlBackend
{
    using System;
    using System.Configuration;
    using System.IO;
    using Messages.CustomChecks;
    using Messages.Heartbeats;
    using Messages.Operations.ServiceControlBackend;
    using NServiceBus;
    using NServiceBus.Config;
    using NServiceBus.MessageInterfaces.MessageMapper.Reflection;
    using NServiceBus.Serializers.Json;
    using NServiceBus.Transports;

    public class ServiceControlBackend : IServiceControlBackend
    {
        public ISendMessages MessageSender { get; set; }

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


            var errorAddress = ConfigureFaultsForwarder.ErrorQueue;
            if (errorAddress != null)
            {
                return new Address("Particular.ServiceControl", errorAddress.Machine);
            }

            if (VersionChecker.CoreVersionIsAtLeast(4,1))
            {
                //audit config was added in 4.1
                Address address;
                if (TryGetAuditAddress(out address))
                {
                    return new Address("Particular.ServiceControl", address.Machine);
                }
            }

            return null;
        }

        static bool TryGetAuditAddress(out Address address)
        {
            var auditConfig = Configure.GetConfigSection<AuditConfig>();
            if (auditConfig != null && !string.IsNullOrEmpty(auditConfig.QueueName))
            {
                var forwardAddress = Address.Parse(auditConfig.QueueName);

                {
                    address = forwardAddress;

                    return true;
                }
            }
            address = null;

            return false;
        }

        MessageMapper messageMapper;
        JsonMessageSerializer serializer;
        Address serviceControlBackendAddress;

    }
}
