namespace ServiceControl.IntegrationDemo
{
    using System;
    using Contracts.MessageFailures;
    using NServiceBus;

    class MessageFailedHandler:IHandleMessages<MessageFailed>
    {
        public void Handle(MessageFailed message)
        {
            Console.Out.WriteLine("Message with id {0} failed with reason {1}",message.FailedMessageId,message.FailureDetails.Exception.Message);
        }
    }
}