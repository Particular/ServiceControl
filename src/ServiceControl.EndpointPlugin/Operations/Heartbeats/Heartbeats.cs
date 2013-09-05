namespace ServiceControl.EndpointPlugin.Operations.Heartbeats
{
    using System;
    using System.Configuration;
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
    using NServiceBus.Unicast;

    public class Heartbeats : Feature, IWantToRunWhenBusStartsAndStops
    {
        public ISendMessages MessageSender { get; set; }

        public IBuilder Builder { get; set; }

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

            serviceControlAddress = GetServiceControlQueue();
            if (serviceControlAddress == null)
            {
                return;
            }

            heartbeatInterval = TimeSpan.FromSeconds(10);
            var interval = ConfigurationManager.AppSettings[@"Heartbeat/Interval"];
            if (!String.IsNullOrEmpty(interval))
            {
                heartbeatInterval = TimeSpan.Parse(interval);
            }

            var mapper = new MessageMapper();

            mapper.Initialize(new[] {typeof(EndpointHeartbeat)});

            serializer = new JsonMessageSerializer(mapper);

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

        static Address GetServiceControlQueue()
        {
            var queueName = ConfigurationManager.AppSettings[@"ServiceControl/Queue"];
            if (!String.IsNullOrEmpty(queueName))
            {
                return Address.Parse(queueName);
            }

            var unicastBus = Configure.Instance.Builder.Build<UnicastBus>();
            var forwardAddress = unicastBus.ForwardReceivedMessagesTo;
            if (forwardAddress != null)
            {
                return new Address("ServiceControl", forwardAddress.Machine);
            }

            var errorAddress = ConfigureFaultsForwarder.ErrorQueue;
            if (errorAddress != null)
            {
                return new Address("ServiceControl", errorAddress.Machine);
            }

            return null;
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

            // Set the TTR to be a factor of 4 of the interval that we expect the hearbeats.
            message.TimeToBeReceived = TimeSpan.FromTicks(heartbeatInterval.Ticks * 4);

            using (var stream = new MemoryStream())
            {
                serializer.Serialize(new object[] {heartBeat}, stream);

                message.Body = stream.ToArray();
            }

            MessageSender.Send(message, serviceControlAddress);
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(Heartbeats));
        Timer heartbeatTimer;
        JsonMessageSerializer serializer;
        Address serviceControlAddress;
        TimeSpan heartbeatInterval;
    }
}