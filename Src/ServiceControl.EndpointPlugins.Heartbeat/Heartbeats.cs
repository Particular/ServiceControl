namespace ServiceControl.EndpointPlugins.Heartbeat
{
    using System;
    using System.Linq;
    using System.Threading;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.ObjectBuilder;

    public class Heartbeats : Feature, IWantToRunWhenBusStartsAndStops
    {
        public IBus Bus { get; set; }

        public IBuilder Builder { get; set; }

        public override void Initialize()
        {
            Configure.Instance.ForAllTypes<IHeartbeatInfoProvider>(t => Configure.Component(t, DependencyLifecycle.InstancePerCall));
        }

        public override bool IsEnabledByDefault
        {
            get { return true; }
        }

        void ExecuteHeartbeat(object state)
        {
            var heartBeat = new EndpointHeartbeat
                {
                    ExecutedAt = DateTime.UtcNow
                };

            Builder.BuildAll<IHeartbeatInfoProvider>().ToList()
                .ForEach(p => p.HeartbeatExecuted(heartBeat));


            Bus.Send(ServiceControlAddress, heartBeat);
        }

        public void Start()
        {
            heartbeatTimer = new Timer(ExecuteHeartbeat, null, TimeSpan.Zero, HeartbeatInterval);
        }


        public void Stop()
        {
            heartbeatTimer.Change(TimeSpan.FromMilliseconds(-1), TimeSpan.FromMilliseconds(-1));
        }


        Address ServiceControlAddress
        {
            get { return Address.Parse("ServiceBus.Management"); }//todo: need to be improved for msmq and rename to ServiceControl when the time comes
        }

        TimeSpan HeartbeatInterval
        {
            get { return TimeSpan.FromSeconds(10); }//todo: make this configurable
        }

        Timer heartbeatTimer;
    }
}