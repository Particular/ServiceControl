namespace ServiceControl.Transports.ASBS
{
    using System;

    public class ConnectionSettings
    {
        public ConnectionSettings(
            AuthenticationSettings authenticationSettings,
            string topicName = default,
            bool useWebSockets = default,
            TimeSpan? queryDelayInterval = default)
        {
            AuthenticationMethod = authenticationSettings;
            TopicName = topicName;
            UseWebSockets = useWebSockets;
            QueryDelayInterval = queryDelayInterval;
        }

        public AuthenticationSettings AuthenticationMethod { get; }

        public TimeSpan? QueryDelayInterval { get; }

        public string TopicName { get; }

        public bool UseWebSockets { get; }
    }
}