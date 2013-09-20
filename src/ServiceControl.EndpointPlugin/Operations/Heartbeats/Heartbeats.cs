namespace ServiceControl.EndpointPlugin.Operations.Heartbeats
{
    using System;
    using System.Configuration;
    using System.Linq;
    using System.Threading;
    using Infrastructure.ServiceControlBackend;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Logging;
    using NServiceBus.ObjectBuilder;
    using NServiceBus.Transports;

    public class Heartbeats : Feature, IWantToRunWhenBusStartsAndStops
    {
        public IServiceControlBackend ServiceControlBackend { get; set; }
        public IBuilder Builder { get; set; }
        public ISendMessages MessageSender { get; set; }

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
            ServiceControlBackend = new ServiceControlBackend(MessageSender, Builder);
            if (ServiceControlBackend.Address == null) return;

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

        public override void Initialize()
        {
            Configure.Instance.ForAllTypes<IHeartbeatInfoProvider>(
                t => Configure.Component(t, DependencyLifecycle.InstancePerCall));
        }

        void ExecuteHeartbeat(object state)
        {
            var heartBeat = new EndpointHeartbeat
            {
                ExecutedAt = DateTime.UtcNow
            };

            Builder.BuildAll<IHeartbeatInfoProvider>().ToList()
                .ForEach(p =>
                {
                    Logger.DebugFormat("Invoking heartbeat provider {0}", p.GetType().FullName);
                    p.HeartbeatExecuted(heartBeat);
                });

            ServiceControlBackend.Send(heartBeat, TimeSpan.FromTicks(heartbeatInterval.Ticks*4));
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(Heartbeats));
        Timer heartbeatTimer;
        TimeSpan heartbeatInterval;
    }
}