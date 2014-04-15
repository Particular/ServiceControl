namespace ServiceControl.HeartbeatMonitoring
{
    using System;
    using System.Threading;
    using Infrastructure;
    using InternalMessages;
    using NServiceBus;

    public class HeartbeatMonitor : IWantToRunWhenBusStartsAndStops
    {
        public IBus Bus { get; set; }

        public HeartbeatStatusProvider HeartbeatStatusProvider { get; set; }
     

        public void Start()
        {
            timer = new Timer(Refresh, null, 0, -1);
        }

        public void Stop()
        {
            using (var manualResetEvent = new ManualResetEvent(false))
            {
                timer.Dispose(manualResetEvent);

                manualResetEvent.WaitOne();
            }
        }

        void Refresh(object _)
        {
            UpdateStatuses();

            try
            {
                timer.Change((int) TimeSpan.FromSeconds(5).TotalMilliseconds, -1);
            }
            catch (ObjectDisposedException)
            {
            }
        }

        void UpdateStatuses()
        {
            var now = DateTime.UtcNow;

            foreach (var failingEndpoint in HeartbeatStatusProvider.GetPotentiallyFailedEndpoints(now))
            {
                var id = DeterministicGuid.MakeId(failingEndpoint.Details.Name, failingEndpoint.Details.HostId.ToString());

                Bus.SendLocal(new RegisterPotentiallyMissingHeartbeats
                {
                    EndpointInstanceId = id,
                    DetectedAt = now,
                    LastHeartbeatAt = failingEndpoint.LastHeartbeatAt
                });
            }
        }

        Timer timer;
    }
}