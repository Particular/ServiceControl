namespace TestDataGenerator
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;

    public class SimpleCommand : ICommand
    {
        public int Index { get; set; }
    }

    class SimpleHandler : IHandleMessages<SimpleCommand>
    {
        EndpointContext endpointContext;

        public SimpleHandler(EndpointContext endpointContext)
        {
            this.endpointContext = endpointContext;
        }

        public Task Handle(SimpleCommand message, IMessageHandlerContext context)
        {
            if (endpointContext.ThrowExceptions)
            {
                throw new Exception("Nope nope nope");
            }
            endpointContext.LogSimpleMessage();
            return Task.CompletedTask;
        }
    }
}