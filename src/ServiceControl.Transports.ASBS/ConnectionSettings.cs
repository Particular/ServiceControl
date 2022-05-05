namespace ServiceControl.Transports.ASBS
{
    using System;

    public class ConnectionSettings
    {
        public ConnectionSettings(
            AuthenticationMethod authenticationSettings,
            string topicName = default,
            bool useWebSockets = default,
            TimeSpan? queryDelayInterval = default)
        {
            AuthenticationMethod = authenticationSettings;
            TopicName = topicName;
            UseWebSockets = useWebSockets;
            QueryDelayInterval = queryDelayInterval;
        }

        public AuthenticationMethod AuthenticationMethod { get; }

        public TimeSpan? QueryDelayInterval { get; }

        public string TopicName { get; }

        public bool UseWebSockets { get; }
    }
}