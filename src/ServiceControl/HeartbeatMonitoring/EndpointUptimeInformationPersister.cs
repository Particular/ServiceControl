namespace ServiceControl.Monitoring
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Threading.Tasks;
    using Particular.HealthMonitoring.Uptime.Api;
    using Raven.Client;


    public class EndpointUptimeInformationPersister : IPersistEndpointUptimeInformation
    {
        const string SingleDocumentId = "EndpointUptime/State";

        readonly IDocumentStore documentStore;
        readonly ConcurrentDictionary<Guid, IHeartbeatEvent> cache = new ConcurrentDictionary<Guid, IHeartbeatEvent>();

        public EndpointUptimeInformationPersister(IDocumentStore documentStore)
        {
            this.documentStore = documentStore;
        }

        public async Task<IHeartbeatEvent[]> Load()
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var state = await session.LoadAsync<HeartbeatState>(SingleDocumentId);
                if (state != null)
                {
                    return state.State.ToArray();
                }
                return new IHeartbeatEvent[0];
            }
        }

        public async Task Store(IHeartbeatEvent @event)
        {
            cache.AddOrUpdate(@event.EndpointInstanceId, @event, (id, old) => @event);

            var state = new HeartbeatState
            {
                State = cache.Values.ToList()
            };

            using (var session = documentStore.OpenAsyncSession())
            {
                await session.StoreAsync(state, SingleDocumentId);
                await session.SaveChangesAsync();
            }
        }
    }
}