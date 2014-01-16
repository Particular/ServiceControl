namespace ServiceControl.Plugin.Heartbeat
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Threading;
    using EndpointPlugin.Operations.ServiceControlBackend;
    using Messages;
    using NServiceBus;
    using NServiceBus.Features;

    class Heartbeats : Feature, IWantToRunWhenBusStartsAndStops
    {
        public ServiceControlBackend ServiceControlBackend { get; set; }

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

            heartbeatInterval = TimeSpan.FromSeconds(10);
            var interval = ConfigurationManager.AppSettings[@"Heartbeat/Interval"];
            if (!String.IsNullOrEmpty(interval))
            {
                heartbeatInterval = TimeSpan.Parse(interval);
            }

            string hostId;
            Dictionary<string, string> hostProperties;

            if(HostInformationRetriever.TryToRetrieveHostInfo(out hostId, out hostProperties))
            {
                SendHostInformationIfAvailable(hostId, hostProperties);
            }

            heartbeatTimer = new Timer(ExecuteHeartbeat, hostId, TimeSpan.Zero, heartbeatInterval);
        }

        void SendHostInformationIfAvailable(string hostId, Dictionary<string, string> hostProperties)
        {
            ServiceControlBackend.Send(new HostInformation {HostId = hostId, Properties = hostProperties});
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
                HostId = hostId.ToString(),
            };

            ServiceControlBackend.Send(heartBeat, TimeSpan.FromTicks(heartbeatInterval.Ticks*4));
        }

        Timer heartbeatTimer;
        TimeSpan heartbeatInterval;
    }
}