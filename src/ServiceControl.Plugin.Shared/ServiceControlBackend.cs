namespace ServiceControl.Plugin
{
    using System;
    using System.Configuration;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using NServiceBus;
    using NServiceBus.Config;
    using NServiceBus.Logging;
    using NServiceBus.Serializers.Binary;
    using NServiceBus.Serializers.Json;
    using NServiceBus.Transports;
   
    class ServiceControlBackend
    {   
        public ServiceControlBackend(ISendMessages messageSender)
        {
            this.messageSender = messageSender;
            serializer = new JsonMessageSerializer(new SimpleMessageMapper());

            serviceControlBackendAddress = GetServiceControlAddress();
        }

      
        public void Send(object messageToSend, TimeSpan timeToBeReceived)
        {
            var message = new TransportMessage
            {
                TimeToBeReceived = timeToBeReceived
            };

            using (var stream = new MemoryStream())
            {
                serializer.Serialize(new[] { messageToSend }, stream);
                message.Body = stream.ToArray();
            }

            //hack to remove the type info from the json
            var bodyString = Encoding.UTF8.GetString(message.Body);

            var toReplace = ", " + messageToSend.GetType().Assembly.GetName().Name;

            bodyString = bodyString.Replace(toReplace, ", ServiceControl");

            message.Body = Encoding.UTF8.GetBytes(bodyString);
            // end hack
            message.Headers[Headers.EnclosedMessageTypes] = messageToSend.GetType().FullName;
            message.Headers[Headers.ContentType] = ContentTypes.Json; //Needed for ActiveMQ transport

            try
            {
                messageSender.Send(message, serviceControlBackendAddress);
            }
            catch (Exception ex)
            {
                Logger.ErrorFormat("This endpoint is unable to contact the ServiceControl Backend to report endpoint information. You have the ServiceControl plugins installed in your endpoint. However, please ensure that the Particular ServiceControl service is installed on this machine, " + 
                                   "or if running ServiceControl on a different machine, then ensure that your endpoint's app.config, AppSettings has the following key set appropriately: ServiceControl/Queue. \r\n" +
                                   @"For example: <add key=""ServiceControl/Queue"" value=""particular.servicecontrol@machine""/>" +
                                   "\r\n Additional details: {0}", ex.ToString());
            }            
        }

        public void Send(object messageToSend)
        {
            Send(messageToSend, TimeSpan.MaxValue);
        }

        static Address GetServiceControlAddress()
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

            if (VersionChecker.CoreVersionIsAtLeast(4, 1))
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

        readonly JsonMessageSerializer serializer;
        readonly ISendMessages messageSender;
        Address serviceControlBackendAddress;
        static readonly ILog Logger = LogManager.GetLogger(typeof(ServiceControlBackend));
    }

    class VersionChecker
    {
        static VersionChecker()
        {
            var fileVersion = FileVersionInfo.GetVersionInfo(typeof(IMessage).Assembly.Location);

            CoreFileVersion = new Version(fileVersion.FileMajorPart, fileVersion.FileMinorPart,
                fileVersion.FileBuildPart);
        }

        public static Version CoreFileVersion { get; set; }

        public static bool CoreVersionIsAtLeast(int major, int minor)
        {
            if (CoreFileVersion.Major > major)
            {
                return true;
            }

            if (CoreFileVersion.Major < major)
            {
                return false;
            }

            return CoreFileVersion.Minor >= minor;
        }
    }
}