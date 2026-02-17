namespace ServiceControl.Transports.ASBS
{
    using System;

    public class ConnectionSettings
    {
        public ConnectionSettings(AuthenticationMethod authenticationSettings,
            string topicName = default,
            bool useWebSockets = default,
            bool enablePartitioning = default,
            TimeSpan? queryDelayInterval = default,
            string hierarchyNamespace = default)
        {
            AuthenticationMethod = authenticationSettings;
            TopicName = topicName;
            UseWebSockets = useWebSockets;
            EnablePartitioning = enablePartitioning;
            QueryDelayInterval = queryDelayInterval;
            HierarchyNamespace = hierarchyNamespace;
        }

        public AuthenticationMethod AuthenticationMethod { get; }

        public TimeSpan? QueryDelayInterval { get; }

        public string TopicName { get; }

        public bool UseWebSockets { get; }

        public bool EnablePartitioning { get; }

        public string HierarchyNamespace { get; }
    }
}