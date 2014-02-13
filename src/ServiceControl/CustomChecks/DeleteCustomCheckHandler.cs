namespace ServiceControl.CustomChecks
{
    using NServiceBus;
    using Raven.Client;

    class DeleteCustomCheckHandler : IHandleMessages<DeleteCustomCheck>
    {
        public IDocumentSession Session { get; set; }

        public IBus Bus { get; set; }

        public void Handle(DeleteCustomCheck message)
        {
            var customCheck = Session.Load<CustomCheck>(message.Id);

            if (customCheck != null)
            {
                Session.Delete(customCheck);
            }

            Bus.Publish(new CustomCheckDeleted {Id = message.Id});
        }
    }
}