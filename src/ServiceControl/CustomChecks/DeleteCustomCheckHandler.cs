namespace ServiceControl.CustomChecks
{
    using System.Threading.Tasks;
    using NServiceBus;
    using Raven.Client;

    class DeleteCustomCheckHandler : IHandleMessages<DeleteCustomCheck>
    {
        public IDocumentStore Store { get; set; }

        public Task Handle(DeleteCustomCheck message, IMessageHandlerContext context)
        {
            Store.DatabaseCommands.Delete(Store.Conventions.DefaultFindFullDocumentKeyFromNonStringIdentifier(message.Id, typeof(CustomCheck), false), null);

            return context.Publish(new CustomCheckDeleted { Id = message.Id });
        }
    }
}