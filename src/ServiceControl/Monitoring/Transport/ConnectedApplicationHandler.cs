namespace ServiceControl.Monitoring.Transport
{
    using System.Threading.Tasks;
    using NServiceBus;
    using ServiceControl.Monitoring;
    using ServiceControl.Persistence;

    public class ConnectedApplicationHandler(IConnectedApplicationsDataStore connectedApplicationsDataStore) : IHandleMessages<ConnectedApplication>
    {
        public Task Handle(ConnectedApplication message, IMessageHandlerContext context)
        {
            _ = connectedApplicationsDataStore.Add(message.Application);

            return Task.CompletedTask;
        }
    }
}
