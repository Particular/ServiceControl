namespace ServiceControl.CustomChecks
{
    using NServiceBus;
    using Raven.Client;

    class DeleteCustomCheckHandler : IHandleMessages<DeleteCustomCheck>
    {
        public IDocumentStore Store { get; set; }

        public IBus Bus { get; set; }

        public void Handle(DeleteCustomCheck message)
        {
            Store.DatabaseCommands.Delete(Store.Conventions.DefaultFindFullDocumentKeyFromNonStringIdentifier(message.Id, typeof(CustomCheck), false), null);

            Bus.Publish(new CustomCheckDeleted {Id = message.Id});
        }
    }
}