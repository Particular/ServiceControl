namespace NServiceBus
{
    using System;
    using System.IO;
    using System.Threading;
    using ServiceControl.LearningTransport;
    using Transports;

    class LearningTransportSubscriptionManager : IManageSubscriptions
    {
        public LearningTransportSubscriptionManager(string basePath, string endpointName, string localAddress)
        {
            this.endpointName = endpointName;
            this.localAddress = localAddress;
            this.basePath = Path.Combine(basePath, ".events");

            Directory.CreateDirectory(this.basePath);
        }

        public void Subscribe(Type eventType, Address publisherAddress)
        {
            var eventDir = GetEventDirectory(eventType);

            // that way we can detect that there is indeed a publisher for the event. That said it also means that we will have do "retries" here due to race condition.
            Directory.CreateDirectory(eventDir);

            var subscriptionEntryPath = GetSubscriptionEntryPath(eventDir);

            var attempts = 0;

            // since we have a design that can run into concurrency exceptions we perform a few retries
            while (true)
            {
                try
                {
                    FileOps.WriteText(subscriptionEntryPath, localAddress);

                    return;
                }
                catch (IOException)
                {
                    attempts++;

                    if (attempts > 10)
                    {
                        throw;
                    }

                    //allow the other task to complete
                    Thread.Sleep(100);
                }
            }
        }

        public void Unsubscribe(Type eventType, Address publisherAddress)
        {
            var eventDir = GetEventDirectory(eventType);
            var subscriptionEntryPath = GetSubscriptionEntryPath(eventDir);

            var attempts = 0;

            // since we have a design that can run into concurrency exceptions we perform a few retries
            while (true)
            {
                try
                {
                    if (!File.Exists(subscriptionEntryPath))
                    {
                        return;
                    }

                    FileOps.Delete(subscriptionEntryPath);

                    return;
                }
                catch (IOException)
                {
                    attempts++;

                    if (attempts > 10)
                    {
                        throw;
                    }

                    //allow the other task to complete
                    Thread.Sleep(100);
                }
            }
        }

        string GetSubscriptionEntryPath(string eventDir) => Path.Combine(eventDir, endpointName + ".subscription");

        string GetEventDirectory(Type eventType) => Path.Combine(basePath, eventType.FullName);

        string basePath;
        string endpointName;
        string localAddress;
    }
}
