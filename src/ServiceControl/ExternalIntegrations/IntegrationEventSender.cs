namespace ServiceControl.ExternalIntegrations
{
    using System;
    using NServiceBus;
    using NServiceBus.Logging;

    public class IntegrationEventSender
    {
        static ILog log = LogManager.GetLogger<IntegrationEventSender>();

        string[] integrationForwardQueues;
        IBus bus;

        public IntegrationEventSender(IBus bus, string[] integrationForwardQueues)
        {
            this.integrationForwardQueues = integrationForwardQueues;
            this.bus = bus;
        }

        public void Send(object evnt)
        {
            foreach (var queue in integrationForwardQueues)
            {
                try
                {
                    bus.Send(Address.Parse(queue), evnt);
                }
                catch (Exception e)
                {
                    log.Error($"Failed dispatching external integration event to {queue}.", e);
                }
            }
            //Maintain backwards compat.
            try
            {
                bus.Publish(evnt);
            }
            catch (Exception e)
            {
                log.Error("Failed publishing external integration event.", e);
            }
        }
    }
}