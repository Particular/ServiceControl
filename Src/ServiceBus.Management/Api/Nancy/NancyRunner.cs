namespace ServiceBus.Management.Api
{
    using NServiceBus;
    using NServiceBus.Logging;
    using Nancy.Hosting.Self;

    public class NancyRunner : IWantToRunWhenBusStartsAndStops
    {
        public NancyHost NancyHost { get; set; }
        public void Start()
        {
            NancyHost.Start();

            Logger.InfoFormat("Api is now accepting requests");
        }

        public void Stop()
        {
            NancyHost.Stop();
            Logger.InfoFormat("Api is now stopped");
        }

        static ILog Logger = LogManager.GetLogger("api");
    }
}