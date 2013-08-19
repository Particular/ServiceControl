namespace ServiceControl.EndpointPlugins.Heartbeat
{
    using System;
    using System.Threading;
    using NServiceBus;
    using NServiceBus.Features;

    public class Heartbeats : Feature, IWantToRunWhenBusStartsAndStops
    {
        public IBus Bus { get; set; }

        public override void Initialize()
        {
            

        }

        public override bool IsEnabledByDefault
        {
            get { return true; }
        }

        void ExecuteHeartbeat(object state)
        {
            Bus.Send(ServiceControlAddress, new EndpointHeartbeat());
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
            get { return TimeSpan.FromSeconds(60); }//todo: make this configurable
        }


        Timer heartbeatTimer;
    }

    public class EndpointHeartbeat
    {
    }
}