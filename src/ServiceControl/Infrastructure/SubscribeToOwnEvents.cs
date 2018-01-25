namespace ServiceControl.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Settings;
    using NServiceBus.Transports;
    using NServiceBus.Unicast;
    using NServiceBus.Unicast.Subscriptions;
    using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;
    using ServiceControl.Contracts.MessageFailures;

    class SubscribeToOwnEvents
    {
        public void Run()
        {
            var localAddress = Settings.LocalAddress();
            var eventTypes = Settings.GetAvailableTypes().Implementing<IEvent>();

            var messageFailureResolvedByRetryType = typeof(MessageFailureResolvedByRetry);

            if (TransportDefinition.HasNativePubSubSupport)
            {
                SubscribeForBrokers(localAddress, eventTypes);

                foreach (var remote in ServiceControlSettings.RemoteInstances)
                {
                    SubscribeForBrokers(Address.Parse(remote.QueueAddress), new []{ messageFailureResolvedByRetryType });
                }
            }
            else
            {
                SubscribeForNonBrokers(localAddress, eventTypes);

                foreach (var remote in ServiceControlSettings.RemoteInstances)
                {
                    MessageSender.Send(
                        message: new TransportMessage(
                            existingId: Guid.NewGuid().ToString(),
                            existingHeaders: new Dictionary<string, string>
                            {
                                { Headers.ControlMessageHeader, string.Empty },
                                { Headers.MessageIntent, MessageIntentEnum.Subscribe.ToString() },
                                { Headers.SubscriptionMessageType, $"{messageFailureResolvedByRetryType.FullName}, Version=1.0.0" }, // keep version stable
                                { Headers.ReplyToAddress, localAddress.ToString() }
                            }),
                        sendOptions: new SendOptions(Address.Parse(remote.QueueAddress)));
                }
            }
        }

        void SubscribeForBrokers(Address address, IEnumerable<Type> eventTypes)
        {
            Parallel.ForEach(eventTypes, eventType => SubscriptionManager.Subscribe(eventType, address));
        }

        void SubscribeForNonBrokers(Address address, IEnumerable<Type> eventTypes)
        {
            SubscriptionStorage.Subscribe(address, eventTypes.Select(e => new MessageType(e)));
        }

        public ISendMessages MessageSender { get; set; }
        public IManageSubscriptions SubscriptionManager { get; set; }
        public ISubscriptionStorage SubscriptionStorage { get; set; }
        public ReadOnlySettings Settings { get; set; }
        public ServiceBus.Management.Infrastructure.Settings.Settings ServiceControlSettings { get; set; }
        public TransportDefinition TransportDefinition { get; set; }
    }
}