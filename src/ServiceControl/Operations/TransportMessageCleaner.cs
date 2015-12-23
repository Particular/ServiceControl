using System;

namespace ServiceControl.Operations
{
    using NServiceBus;

    static class TransportMessageCleaner
    {
        public static void CleanForForwarding(TransportMessage message)
        {
            message.TimeToBeReceived = TimeSpan.MaxValue;
        }
    }
}
