namespace ServiceControl.EndpointPlugin.Heartbeats
{
    using System;
    using System.Configuration;
    using System.Threading;
    using Messages.Heartbeats;
    using NServiceBus;
    using NServiceBus.Features;
    using Operations.ServiceControlBackend;

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

            heartbeatTimer = new Timer(ExecuteHeartbeat, null, TimeSpan.Zero, heartbeatInterval);
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

        void ExecuteHeartbeat(object state)
        {
            var heartBeat = new EndpointHeartbeat
            {
                ExecutedAt = DateTime.UtcNow
            };

            ServiceControlBackend.Send(heartBeat, TimeSpan.FromTicks(heartbeatInterval.Ticks*4));
        }

        Timer heartbeatTimer;
        TimeSpan heartbeatInterval;
    }
}