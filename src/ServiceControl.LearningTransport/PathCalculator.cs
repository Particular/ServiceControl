namespace ServiceControl.LearningTransport
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    class PathCalculator
    {
        public PathCalculator(string basePath)
        {
            this.basePath = basePath;
        }

        public EndpointBasePaths PathsForEndpoint(string endpoint)
        {
            var queuePath = Path.Combine(basePath, endpoint);

            return new EndpointBasePaths
            {
                Header = queuePath,
                Body = Path.Combine(queuePath, BodyDirName),
                Deferred = Path.Combine(queuePath, DelayedDirName),
                Pending = Path.Combine(queuePath, PendingDirName),
                Committed = Path.Combine(queuePath, CommittedDirName)
            };
        }

        public MessageBasePaths PathsForDispatch(string endpoint)
        {
            var endpointPaths = PathsForEndpoint(endpoint);

            return new MessageBasePaths
            {
                Header = endpointPaths.Header,
                Body = endpointPaths.Body
            };
        }

        public MessageBasePaths PathsForDispatch(string endpoint, DateTime timeToDeliver)
        {
            var endpointPaths = PathsForEndpoint(endpoint);
            return new MessageBasePaths
            {
                Header = Path.Combine(endpointPaths.Deferred, timeToDeliver.ToString("yyyyMMddHHmmss")),
                Body = endpointPaths.Body
            };
        }
        public IEnumerable<MessageBasePaths> GetSubscribersFor(IEnumerable<Type> allEventTypes)
        {
            var subscribers = new HashSet<MessageBasePaths>();

            foreach (var eventType in allEventTypes)
            {
                if (eventType.FullName == null)
                {
                    continue;
                }
                var subscriptionsPath = Path.Combine(basePath, EventsDirName, eventType.FullName);

                if (!Directory.Exists(subscriptionsPath))
                {
                    continue;
                }

                foreach (var subscription in Directory.GetFiles(subscriptionsPath))
                {
                    var subscriber = FileOps.ReadText(subscription);

                    subscribers.Add(PathsForDispatch(subscriber));
                }
            }

            return subscribers;
        }

        public struct EndpointBasePaths
        {
            public string Header;
            public string Body;
            public string Deferred;
            public string Committed;
            public string Pending;
        }

        public struct MessageBasePaths
        {
            public string Header;
            public string Body;
        }

        readonly string basePath;

        const string EventsDirName = ".events";

        public const string BodyFileSuffix = ".body.txt";
        const string BodyDirName = ".bodies";
        const string DelayedDirName = ".delayed";

        const string CommittedDirName = ".committed";
        const string PendingDirName = ".pending";
    }
}
