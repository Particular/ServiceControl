namespace ServiceControl.Monitoring.Http.Diagrams
{
    using System;
    using System.Linq;
    using System.Reflection;
    using NServiceBus.Logging;

    public static class MonitoredEndpointMessageTypeParser
    {
        public static MonitoredEndpointMessageType Parse(string typeName)
        {
            //HINT: the value we get is either empty or equals EnclosedMessageTypes header value.
            //      What it means that when non empty the value is either TypeName or AssemblyQualifiedName
            //      see: https://docs.particular.net/nservicebus/messaging/headers#serialization-headers-nservicebus-enclosedmessagetypes

            if (string.IsNullOrEmpty(typeName))
                return new MonitoredEndpointMessageType();

            var commaIndex = typeName.IndexOf(",", StringComparison.InvariantCulture);

            if (commaIndex != -1)
            {
                var textBeforeComma = typeName.Substring(0, commaIndex);
                var textAfterComma = typeName.Substring(commaIndex + 1);

                try
                {
                    var assemblyName = new AssemblyName(textAfterComma);

                    return new MonitoredEndpointMessageType
                    {
                        Id = typeName,
                        TypeName = textBeforeComma,
                        AssemblyName = assemblyName.Name,
                        AssemblyVersion = assemblyName.Version.ToString(),
                        Culture = assemblyName.CultureName,
                        PublicKeyToken = string.Concat(assemblyName.GetPublicKeyToken().Select(b => b.ToString("x2")))
                    };
                }
                catch (Exception e)
                {
                    Logger.Debug($"Error parsing message type: {typeName}.", e);
                }
            }

            return new MonitoredEndpointMessageType
            {
                Id = typeName,
                TypeName = typeName
            };
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(MonitoredEndpointMessageTypeParser));
    }
}