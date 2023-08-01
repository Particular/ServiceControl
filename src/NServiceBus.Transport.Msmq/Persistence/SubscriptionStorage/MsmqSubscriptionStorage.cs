namespace NServiceBus.Persistence.Msmq
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Logging;
    using Transport.Msmq;
    using Unicast.Subscriptions.MessageDrivenSubscriptions;
    using MessageType = Unicast.Subscriptions.MessageType;

    class MsmqSubscriptionStorage : ISubscriptionStorage, IDisposable
    {
        public MsmqSubscriptionStorage(IMsmqSubscriptionStorageQueue storageQueue)
        {
            this.storageQueue = storageQueue;

            // Required to be lazy loaded as the queue might not exist yet
            lookup = new Lazy<Dictionary<Subscriber, Dictionary<MessageType, string>>>(CreateLookup);
        }

        public void Dispose()
        {
            // Filled in by Janitor.fody
        }

        Dictionary<Subscriber, Dictionary<MessageType, string>> CreateLookup()
        {
            var output = new Dictionary<Subscriber, Dictionary<MessageType, string>>(SubscriberComparer);

            var messages = storageQueue.GetAllMessages()
                .OrderByDescending(m => m.ArrivedTime)
                .ThenBy(x => x.Id) // ensure same order of messages with same timestamp across all endpoints
                .ToArray();

            foreach (var m in messages)
            {
                var messageTypeString = m.Body as string;
                var messageType = new MessageType(messageTypeString); //this will parse both 2.6 and 3.0 type strings
                var subscriber = Deserialize(m.Label);

                if (!output.TryGetValue(subscriber, out var endpointSubscriptions))
                {
                    output[subscriber] = endpointSubscriptions = new Dictionary<MessageType, string>();
                }

                if (endpointSubscriptions.ContainsKey(messageType))
                {
                    // this message is stale and can be removed
                    storageQueue.TryReceiveById(m.Id);
                }
                else
                {
                    endpointSubscriptions[messageType] = m.Id;
                }
            }

            return output;
        }

        public Task<IEnumerable<Subscriber>> GetSubscriberAddressesForMessage(IEnumerable<MessageType> messageTypes, ContextBag context)
        {
            var messagelist = messageTypes.ToArray();
            var result = new HashSet<Subscriber>();

            try
            {
                // note: ReaderWriterLockSlim has a thread affinity and cannot be used with await!
                rwLock.EnterReadLock();

                foreach (var subscribers in lookup.Value)
                {
                    foreach (var messageType in messagelist)
                    {
                        if (subscribers.Value.TryGetValue(messageType, out _))
                        {
                            result.Add(subscribers.Key);
                        }
                    }
                }
            }
            finally
            {
                rwLock.ExitReadLock();
            }

            return Task.FromResult<IEnumerable<Subscriber>>(result);
        }

        public Task Subscribe(Subscriber subscriber, MessageType messageType, ContextBag context)
        {
            var body = $"{messageType.TypeName}, Version={messageType.Version}";
            var label = Serialize(subscriber);
            var messageId = storageQueue.Send(body, label);

            AddToLookup(subscriber, messageType, messageId);

            log.DebugFormat($"Subscriber {subscriber.TransportAddress} added for message {messageType}.");

            return TaskEx.CompletedTask;
        }

        public Task Unsubscribe(Subscriber subscriber, MessageType messageType, ContextBag context)
        {
            var messageId = RemoveFromLookup(subscriber, messageType);

            if (messageId != null)
            {
                storageQueue.TryReceiveById(messageId);
            }

            log.Debug($"Subscriber {subscriber.TransportAddress} removed for message {messageType}.");

            return TaskEx.CompletedTask;
        }

        static string Serialize(Subscriber subscriber)
        {
            return $"{subscriber.TransportAddress}|{subscriber.Endpoint}";
        }

        static Subscriber Deserialize(string serializedForm)
        {
            var parts = serializedForm.Split(EntrySeparator, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || parts.Length > 2)
            {
                log.Error($"Invalid format of subscription entry: {serializedForm}.");
                return null;
            }
            var endpointName = parts.Length > 1
                ? parts[1]
                : null;

            return new Subscriber(parts[0], endpointName);
        }

        void AddToLookup(Subscriber subscriber, MessageType typeName, string messageId)
        {
            try
            {
                // note: ReaderWriterLockSlim has a thread affinity and cannot be used with await!
                rwLock.EnterWriteLock();

                if (!lookup.Value.TryGetValue(subscriber, out var dictionary))
                {
                    dictionary = new Dictionary<MessageType, string>();
                }
                else
                {
                    // replace existing subscriber
                    lookup.Value.Remove(subscriber);
                }

                dictionary[typeName] = messageId;
                lookup.Value[subscriber] = dictionary;
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }

        string RemoveFromLookup(Subscriber subscriber, MessageType typeName)
        {
            try
            {
                // note: ReaderWriterLockSlim has a thread affinity and cannot be used with await!
                rwLock.EnterWriteLock();

                if (lookup.Value.TryGetValue(subscriber, out var subscriptions))
                {
                    if (subscriptions.TryGetValue(typeName, out var messageId))
                    {
                        subscriptions.Remove(typeName);
                        if (subscriptions.Count == 0)
                        {
                            lookup.Value.Remove(subscriber);
                        }

                        return messageId;
                    }
                }
            }
            finally
            {
                rwLock.ExitWriteLock();
            }

            return null;
        }

        Lazy<Dictionary<Subscriber, Dictionary<MessageType, string>>> lookup;
        IMsmqSubscriptionStorageQueue storageQueue;
        ReaderWriterLockSlim rwLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

        static ILog log = LogManager.GetLogger(typeof(ISubscriptionStorage));
        static TransportAddressEqualityComparer SubscriberComparer = new TransportAddressEqualityComparer();

        static readonly char[] EntrySeparator =
        {
            '|'
        };

        sealed class TransportAddressEqualityComparer : IEqualityComparer<Subscriber>
        {
            public bool Equals(Subscriber x, Subscriber y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                if (x is null)
                {
                    return false;
                }

                if (y is null)
                {
                    return false;
                }

                if (x.GetType() != y.GetType())
                {
                    return false;
                }

                return string.Equals(x.TransportAddress, y.TransportAddress, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(Subscriber obj)
            {
                return obj.TransportAddress != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(obj.TransportAddress) : 0;
            }
        }
    }
}
