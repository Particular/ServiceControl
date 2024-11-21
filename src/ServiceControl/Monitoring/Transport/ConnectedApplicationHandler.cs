namespace ServiceControl.Monitoring.Transport
{
    using System.Threading.Tasks;
    using NServiceBus;
    using ServiceControl.Persistence;
    using ConnectedApplication = ConnectedApplication;

    public class ConnectedApplicationHandler(IConnectedApplicationsDataStore connectedApplicationsDataStore) : IHandleMessages<ConnectedApplication>
    {
        public async Task Handle(ConnectedApplication message, IMessageHandlerContext context)
        {
            var connectedApplication = new Persistence.ConnectedApplication
            {
                Name = message.Application,
                SupportsHeartbeats = message.SupportsHeartbeats
            };
            await connectedApplicationsDataStore.UpdateConnectedApplication(connectedApplication, context.CancellationToken);
        }
    }
}
