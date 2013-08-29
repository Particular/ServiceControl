namespace ServiceControl.EndpointPlugin.Operations.Heartbeats
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
    using NServiceBus.Serializers.Json;
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
            get { return Address.Parse("ServiceControl"); }
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

            var mapper = new MessageMapper();

            mapper.Initialize(new[] {typeof(EndpointHeartbeat)});

            serializer = new JsonMessageSerializer(mapper);

            heartbeatTimer = new Timer(ExecuteHeartbeat, null, TimeSpan.Zero, HeartbeatInterval);
        }

        public void Stop()
        {
            if (!Enabled)
            {
                return;
            }

            var manualResetEvent = new ManualResetEvent(false);

            heartbeatTimer.Dispose(manualResetEvent);

            manualResetEvent.WaitOne();
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

            var message = new TransportMessage();

            using (var stream = new MemoryStream())
            {
                serializer.Serialize(new object[] {heartBeat}, stream);

                message.Body = stream.ToArray();
            }

            MessageSender.Send(message, ServiceControlAddress);
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(Heartbeats));
        Timer heartbeatTimer;
        JsonMessageSerializer serializer;
    }
}