namespace ServiceControl.EndpointControl.Handlers
{
    using System;
    using System.Linq;
    using InternalMessages;
    using NServiceBus;
    using Raven.Client;
    using ServiceControl.Contracts.EndpointControl;

    public class RegisterEndpointHandler : IHandleMessages<RegisterEndpoint>
    {
        public IDocumentStore Store { get; set; }
        public KnownEndpointsCache EndpointsCache { get; set; }
        public IBus Bus { get; set; }

        public void Handle(RegisterEndpoint message)
        {
            var id = message.EndpointInstanceId;

            //Injecting store in this class because we want to know about ConcurrencyExceptions so that EndpointsCache.MarkAsProcessed is not called if the save fails.
            using (var session = Store.OpenSession()) 
            {
                session.Advanced.UseOptimisticConcurrency = true;

                KnownEndpoint knownEndpoint;

                if (id == Guid.Empty)
                {
                    knownEndpoint = session.Query<KnownEndpoint>().SingleOrDefault(e => e.EndpointDetails.Name == message.Endpoint.Name && e.EndpointDetails.Host == message.Endpoint.Host);
                }
                else
                {
                    knownEndpoint = session.Load<KnownEndpoint>(id);

                    if (knownEndpoint == null)
                    {
                        knownEndpoint = session.Query<KnownEndpoint>().SingleOrDefault(e => e.HasTemporaryId && e.EndpointDetails.Name == message.Endpoint.Name && e.EndpointDetails.Host == message.Endpoint.Host);
                    }
                }
               
                if (knownEndpoint == null)
                {
                    //new endpoint
                    Bus.Publish(new NewEndpointDetected
                    {
                        Endpoint = message.Endpoint,
                        DetectedAt = message.DetectedAt
                    });

                    knownEndpoint = new KnownEndpoint
                    {
                        EndpointDetails = message.Endpoint,
                        HostDisplayName = message.Endpoint.Host,
                    };

                    if (id == Guid.Empty)
                    {
                        knownEndpoint.Id = Guid.NewGuid();
                        knownEndpoint.HasTemporaryId = true;
                    }
                    else
                    {
                        knownEndpoint.Id = id;
                    }
                }
                else
                {
                    if (knownEndpoint.HasTemporaryId && id != Guid.Empty)
                    {
                        session.Delete(knownEndpoint);
                        session.Store(new KnownEndpoint
                        {
                            Id = id,
                            EndpointDetails = message.Endpoint,
                            HostDisplayName = message.Endpoint.Host,
                        });
                    }
                }

                session.Store(knownEndpoint);
                session.SaveChanges();
            }

            EndpointsCache.MarkAsProcessed(message.Endpoint.HostId);
        }
    }
}