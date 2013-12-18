namespace Server
{
    using System;
    using NServiceBus;

    class MessageSender : IWantToRunWhenBusStartsAndStops
    {
        public IBus Bus { get; set; }

        public void Start()
        {
            Bus.SendLocal(new MyMessage
                {
                    SomeId = Guid.NewGuid()
                });
        }

        public void Stop()
        {
        }
    }
}