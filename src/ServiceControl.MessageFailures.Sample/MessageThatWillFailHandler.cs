namespace ServiceControl.MessageFailures.Sample
{
    using System;
    using NServiceBus;

    class MessageThatWillFailHandler : IHandleMessages<MessageThatWillFail>
    {
        public void Handle(MessageThatWillFail message)
        {
            if (!Succeed)
                throw new Exception("Faked exception from the Failure sample");


            Console.Out.WriteLine("Message proceseed OK");
        }

        public static bool Succeed { get; set; }
    }
}