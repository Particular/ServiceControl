namespace ServiceControl.CustomChecks
{
    using NServiceBus;
    using Raven.Client;
    using ServiceControl.Infrastructure.DomainEvents;

    class DeleteCustomCheckHandler : IHandleMessages<DeleteCustomCheck>
    {
        public IDocumentStore Store { get; set; }

        public void Handle(DeleteCustomCheck message)
        {
            Store.DatabaseCommands.Delete(Store.Conventions.DefaultFindFullDocumentKeyFromNonStringIdentifier(message.Id, typeof(CustomCheck), false), null);

            DomainEvents.Raise(new CustomCheckDeleted { Id = message.Id });
        }
    }
}