namespace ServiceControl.EndpointPlugin.Infrastructure.Heartbeats
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Logging;
    using NServiceBus.MessageInterfaces.MessageMapper.Reflection;
    using NServiceBus.ObjectBuilder;
    using NServiceBus.Serializers.XML;
    using NServiceBus.Transports;

    public class Heartbeats : Feature, IWantToRunWhenBusStartsAndStops
    {
        public ISendMessages MessageSender { get; set; }


        public IBuilder Builder { get; set; }

        public override bool IsEnabledByDefault
        {
            get { return true; }
        }


        Address ServiceControlAddress
        {
            get { return Address.Parse("ServiceBus.Management"); }
            //todo: need to be improved for msmq and rename to ServiceControl when the time comes
        }

        TimeSpan HeartbeatInterval
        {
            get { return TimeSpan.FromSeconds(10); } //todo: make this configurable
        }

        public void Start()
        {
            if (!Enabled)
            {
                return;
            }
            heartbeatTimer = new Timer(ExecuteHeartbeat, null, TimeSpan.Zero, HeartbeatInterval);
        }


        public void Stop()
        {
            if (!Enabled)
            {
                return;
            }

            heartbeatTimer.Change(TimeSpan.FromMilliseconds(-1), TimeSpan.FromMilliseconds(-1));
        }

        public override void Initialize()
        {
            serializer = new XmlMessageSerializer(new MessageMapper());

            serializer.InitType(typeof(EndpointHeartbeat));

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

            var message = new TransportMessage();

            using (var stream = new MemoryStream())
            {
                serializer.Serialize(new[] {heartBeat}, stream);

                stream.Position = 0;
                message.Body = new byte[stream.Length];
                stream.Read(message.Body, 0, Convert.ToInt32(stream.Length));
            }
            
            MessageSender.Send(message,ServiceControlAddress);
        }

        static XmlMessageSerializer serializer;//change this when we switch to json for ServiceControl
        static readonly ILog Logger = LogManager.GetLogger(typeof(Heartbeats));
        Timer heartbeatTimer;
    }
}