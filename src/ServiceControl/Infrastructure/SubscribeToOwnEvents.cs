namespace ServiceControl.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Extensibility;
    using NServiceBus.Routing;
    using NServiceBus.Settings;
    using NServiceBus.Transport;
    using NServiceBus.Unicast.Subscriptions;
    using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;
    using ServiceControl.Contracts.EndpointControl;
    using ServiceControl.Contracts.MessageFailures;

    class SubscribeToOwnEvents
    {
        public async Task Run()
        {
            var localAddress = Settings.LocalAddress();
            var eventTypes = Settings.GetAvailableTypes().Implementing<IEvent>();

            var typesToSubscribeTo = new[]
            {
                typeof(MessageFailureResolvedByRetry),
                typeof(NewEndpointDetected)
            };

            var transportInfrastructure = Settings.Get<TransportInfrastructure>();
            
            if (transportInfrastructure.OutboundRoutingPolicy.Publishes == OutboundRoutingType.Multicast)
            {
                var transportSubscriptionInfrastructure = transportInfrastructure.ConfigureSubscriptionInfrastructure();
                var propertyInfo = typeof(TransportSubscriptionInfrastructure).GetProperty("SubscriptionManagerFactory", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                var subscriptionManagerFactory = (Func<IManageSubscriptions>)propertyInfo.GetMethod.Invoke(transportSubscriptionInfrastructure, null);
                SubscriptionManager = subscriptionManagerFactory();
                
                await SubscribeForBrokers(localAddress, eventTypes).ConfigureAwait(false);

                foreach (var remote in ServiceControlSettings.RemoteInstances)
                {
                    await SubscribeForBrokers(remote.QueueAddress, typesToSubscribeTo);
                }
            }
            else
            {
                await SubscribeForNonBrokers(localAddress, eventTypes).ConfigureAwait(false);

                foreach (var remote in ServiceControlSettings.RemoteInstances)
                {
                    var operations = new List<TransportOperation>();
                    foreach (var typeToSubscribeTo in typesToSubscribeTo)
                    {
                        operations.Add(new TransportOperation(new OutgoingMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>
                        {
                            {Headers.ControlMessageHeader, string.Empty},
                            {Headers.MessageIntent, MessageIntentEnum.Subscribe.ToString()},
                            {Headers.SubscriptionMessageType, $"{typeToSubscribeTo.FullName}, Version=1.0.0"}, // keep version stable
                            {Headers.ReplyToAddress, localAddress}
                        }, new byte[0]), new UnicastAddressTag(remote.QueueAddress)));
                    }

                    await MessageDispatcher.Dispatch(new TransportOperations(operations.ToArray()), new TransportTransaction(), new ContextBag())
                        .ConfigureAwait(false);       
                }
            }
        }

        Task SubscribeForBrokers(string address, IEnumerable<Type> eventTypes)
        {
            return Task.WhenAll(eventTypes.Select(eventType => SubscriptionManager.Subscribe(eventType, new ContextBag())));
        }

        Task SubscribeForNonBrokers(string localAddress, IEnumerable<Type> eventTypes)
        {
            return Task.WhenAll(eventTypes.Select(eventType => SubscriptionStorage.Subscribe(new Subscriber(localAddress, Settings.EndpointName()), new MessageType(eventType), new ContextBag())));
        }

        public IDispatchMessages MessageDispatcher { get; set; }
        public IManageSubscriptions SubscriptionManager { get; set; }
        public ISubscriptionStorage SubscriptionStorage { get; set; }
        public ReadOnlySettings Settings { get; set; }
        public ServiceBus.Management.Infrastructure.Settings.Settings ServiceControlSettings { get; set; }
    }
}