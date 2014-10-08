namespace ServiceControl.IntegrationDemo
{
    using System;
    using NServiceBus;
    using ServiceControl.Contracts;

    class MessageFailedHandler:IHandleMessages<MessageFailed>
    {
        public void Handle(MessageFailed message)
        {
            Console.Out.WriteLine("Message with id {0} failed with reason {1}",message.FailedMessageId,message.FailureDetails.Exception.Message);

            //if more info about the exception is needed you need to make calls to the http api
          
            //eg:  /api/errors/{message.message.FailedMessageId}   #returns full metadata for message
        }
    }
}