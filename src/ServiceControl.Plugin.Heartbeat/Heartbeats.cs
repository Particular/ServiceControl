namespace ServiceControl.Plugin.Heartbeat
{
    using System;
    using System.Configuration;
    using System.Threading;
    using EndpointPlugin.Operations.ServiceControlBackend;
    using Messages;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Transports;

    class Heartbeats : Feature, IWantToRunWhenBusStartsAndStops
    {
        public ISendMessages MessageSender{ get; set; }

        public override bool IsEnabledByDefault
        {
            get { return true; }
        }

        public void Start()
        {
            if (!Enabled)
            {
                return;
            }
            backend = new ServiceControlBackend(MessageSender);
            heartbeatInterval = TimeSpan.FromSeconds(10);
            var interval = ConfigurationManager.AppSettings[@"Heartbeat/Interval"];
            if (!String.IsNullOrEmpty(interval))
            {
                heartbeatInterval = TimeSpan.Parse(interval);
            }
            
            var hostInfo = HostInformationRetriever.RetrieveHostInfo();

            SendStartupMessageToBackend(hostInfo);

            heartbeatTimer = new Timer(ExecuteHeartbeat, hostInfo.HostId, TimeSpan.Zero, heartbeatInterval);
        }

        void SendStartupMessageToBackend(HostInformation hostInfo)
        {
            backend.Send(new RegisterEndpointStartup
            {
                HostId = hostInfo.HostId, 
                Endpoint = Configure.EndpointName,
                HostDisplayName = hostInfo.DisplayName,
                HostProperties = hostInfo.Properties,
                StartedAt = DateTime.UtcNow
            });
        }

        public void Stop()
        {
            if (!Enabled)
            {
                return;
            }

            if (heartbeatTimer == null)
            {
                return;
            }

            using (var manualResetEvent = new ManualResetEvent(false))
            {
                heartbeatTimer.Dispose(manualResetEvent);

                manualResetEvent.WaitOne();
            }
        }

        void ExecuteHeartbeat(object hostId)
        {
            var heartBeat = new EndpointHeartbeat
            {
                ExecutedAt = DateTime.UtcNow,
                Endpoint = Configure.EndpointName,
                HostId = (Guid)hostId
            };

            backend.Send(heartBeat, TimeSpan.FromTicks(heartbeatInterval.Ticks * 4));
        }

        ServiceControlBackend backend;
        Timer heartbeatTimer;
        TimeSpan heartbeatInterval;
    }
}