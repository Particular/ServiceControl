namespace ServiceControl.IntegrationDemo
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using ServiceControl.Contracts;

    class MessageFailedHandler : IHandleMessages<MessageFailed>
    {
        public Task Handle(MessageFailed message, IMessageHandlerContext context)
        {
            Console.Out.WriteLine("Message with id {0} failed with reason {1}", message.FailedMessageId, message.FailureDetails.Exception.Message);

            return Task.FromResult(0);
            //if more info about the exception is needed you need to make calls to the http api

            //eg:  /api/errors/{message.message.FailedMessageId}   #returns full metadata for message
        }
    }
}