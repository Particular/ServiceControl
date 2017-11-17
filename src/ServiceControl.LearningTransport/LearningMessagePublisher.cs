namespace ServiceControl.LearningTransport
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus;
    using NServiceBus.Transports;
    using NServiceBus.Unicast;

    class LearningMessagePublisher : IPublishMessages
    {
        public LearningMessagePublisher(MessageDispatcher dispatcher, PathCalculator pathCalculator)
        {
            this.dispatcher = dispatcher;
            this.pathCalculator = pathCalculator;
        }

        public void Publish(TransportMessage message, PublishOptions publishOptions)
        {
            var replyToAddress = publishOptions.ReplyToAddress ?? message.ReplyToAddress;

            var allEventTypes = GetPotentialEventTypes(publishOptions.EventType);

            var subscribers = pathCalculator.GetSubscribersFor(allEventTypes);

            foreach (var subscriberPaths in subscribers)
            {
                dispatcher.Dispatch(message, subscriberPaths, replyToAddress.Queue, publishOptions.EnlistInReceiveTransaction);
            }
        }

        static IEnumerable<Type> GetPotentialEventTypes(Type messageType)
        {
            var allEventTypes = new HashSet<Type>();

            var currentType = messageType;

            while (currentType != null)
            {
                //do not include the marker interfaces
                if (IsCoreMarkerInterface(currentType))
                {
                    break;
                }

                allEventTypes.Add(currentType);

                currentType = currentType.BaseType;
            }

            foreach (var type in messageType.GetInterfaces().Where(i => !IsCoreMarkerInterface(i)))
            {
                allEventTypes.Add(type);
            }

            return allEventTypes;
        }

        static bool IsCoreMarkerInterface(Type type) => type == typeof(IMessage) || type == typeof(IEvent) || type == typeof(ICommand);

        private readonly MessageDispatcher dispatcher;
        private readonly PathCalculator pathCalculator;
    }
}
