namespace ServiceControl.Transports.ASBS
{
    public class ConnectionSettings
    {
        public ConnectionSettings(
            string transportConnectionString = default,
            bool useManagedIdentity = default,
            string fullyQualifiedNamespace = default,
            string clientId = default,
            string topicName = default,
            bool useWebSockets = default,
            bool useDefaultCredentials = default)
        {
            TransportConnectionString = transportConnectionString;
            UseManagedIdentity = useManagedIdentity;
            FullyQualifiedNamespace = fullyQualifiedNamespace;
            ClientId = clientId;
            TopicName = topicName;
            UseWebSockets = useWebSockets;
            UseDefaultCredentials = useDefaultCredentials;
        }

        public string TransportConnectionString { get; private set; }
        public bool UseManagedIdentity { get; private set; }
        public bool UseDefaultCredentials { get; private set; }
        public string FullyQualifiedNamespace { get; private set; }
        public string ClientId { get; private set; }
        public string TopicName { get; private set; }
        public bool UseWebSockets { get; private set; }
    }
}