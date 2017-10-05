namespace ServiceControl.LearningTransport
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Threading;
    using NServiceBus.Logging;

    class DelayedMessagePoller
    {
        public DelayedMessagePoller(PathCalculator.EndpointBasePaths endpointPaths)
        {
            this.endpointPaths = endpointPaths;
            timer = new Timer(MoveDelayedMessagesToMainDirectory);
        }

        void MoveDelayedMessagesToMainDirectory(object state)
        {
            try
            {
                foreach (var delayDir in new DirectoryInfo(endpointPaths.Deferred).EnumerateDirectories())
                {
                    var timeToTrigger = DateTime.ParseExact(delayDir.Name, "yyyyMMddHHmmss", DateTimeFormatInfo.InvariantInfo);

                    if (DateTime.UtcNow >= timeToTrigger)
                    {
                        foreach (var fileInfo in delayDir.EnumerateFiles())
                        {
                            FileOps.Move(fileInfo.FullName, Path.Combine(endpointPaths.Header, fileInfo.Name));
                        }
                    }

                    //wait a bit more so we can safely delete the dir
                    if (DateTime.UtcNow >= timeToTrigger.AddSeconds(10))
                    {
                        Directory.Delete(delayDir.FullName);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Unable to move expired messages to main input queue.", ex);
            }
        }

        public void Start()
        {
            timer.Change(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }

        public void Stop()
        {
            timer.Dispose();
        }

        readonly PathCalculator.EndpointBasePaths endpointPaths;

        Timer timer;

        static ILog Logger = LogManager.GetLogger<DelayedMessagePoller>();
    }
}
