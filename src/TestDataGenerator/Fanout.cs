namespace TestDataGenerator
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;

    public class FanoutCommand : ICommand
    {
        public int Level { get; set; }
        public int Index { get; set; }
    }

    class FanoutHandler : IHandleMessages<FanoutCommand>
    {
        EndpointContext endpointContext;

        public FanoutHandler(EndpointContext endpointContext)
        {
            this.endpointContext = endpointContext;
        }

        public async Task Handle(FanoutCommand message, IMessageHandlerContext context)
        {
            if (endpointContext.ThrowExceptions)
            {
                throw new Exception("Uh-uh-uh, you didn't say the magic word.");
            }

            var nextLevel = message.Level + 1;
            if (nextLevel < Program.EndpointCount)
            {
                var destination = $"Endpoint{nextLevel}";
                await context.Send(destination, new FanoutCommand { Level = nextLevel, Index = 0 });
                await context.Send(destination, new FanoutCommand { Level = nextLevel, Index = 1 });
            }
        }
    }
}