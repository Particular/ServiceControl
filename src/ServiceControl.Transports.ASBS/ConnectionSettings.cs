namespace ServiceControl.Transports.ASBS
{
    public class ConnectionSettings
    {
        public ConnectionSettings(
            string connectionString = default,
            bool useManagedIdentity = default,
            string fullyQualifiedNamespace = default,
            string clientId = default,
            string topicName = default,
            bool useWebSockets = default)
        {
            ConnectionString = connectionString;
            UseManagedIdentity = useManagedIdentity;
            FullyQualifiedNamespace = fullyQualifiedNamespace;
            ClientId = clientId;
            TopicName = topicName;
            UseWebSockets = useWebSockets;
        }

        public string ConnectionString { get; private set; }
        public bool UseManagedIdentity { get; private set; }
        public string FullyQualifiedNamespace { get; private set; }
        public string ClientId { get; private set; }
        public string TopicName { get; private set; }
        public bool UseWebSockets { get; private set; }
    }
}