namespace ServiceControl.Infrastructure.RavenDB
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using NServiceBus;
    using NServiceBus.Configuration.AdvanceExtensibility;
    using NServiceBus.Persistence;
    using Raven.Abstractions.Extensions;
    using Raven.Abstractions.Replication;
    using Raven.Client;
    using Raven.Client.Document;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.CompositeViews.Endpoints;
    using ServiceControl.EndpointControl;
    using ServiceControl.Infrastructure.RavenDB.Subscriptions;

    public class RavenBootstrapper : INeedInitialization
    {
        public void Customize(BusConfiguration configuration)
        {
            var documentStore = configuration.GetSettings().Get<DocumentStore>("ServiceControl.DocumentStore");
            var settings = configuration.GetSettings().Get<Settings>("ServiceControl.Settings");

            StartRaven(documentStore, settings);
            
            configuration.UsePersistence<CachedRavenDBPersistence, StorageType.Subscriptions>();
        }

        void StartRaven(DocumentStore documentStore, Settings settings)
        {
            documentStore.Url = settings.StorageUrl;
            documentStore.DefaultDatabase = "ServiceControl";
            documentStore.EnlistInDistributedTransactions = false;
            documentStore.Conventions.SaveEnumsAsIntegers = true;
            documentStore.Conventions.FailoverBehavior = FailoverBehavior.FailImmediately; // This prevents the client from looking for replica servers
            documentStore.Credentials = CredentialCache.DefaultNetworkCredentials;
            if (documentStore.HttpMessageHandlerFactory == null)
            {
                documentStore.HttpMessageHandlerFactory = () => new WebRequestHandler
                {
                    ServerCertificateValidationCallback = (obj, certificate, chain, errors) => true, //Allow Self Signing certs.  Since we are both client and server this is safe
                    UnsafeAuthenticatedConnectionSharing = true, // This is needed according to https://groups.google.com/d/msg/ravendb/DUYFvqWR5Hc/l1sKE5A1mVgJ
                    PreAuthenticate = true,
                    Credentials = CredentialCache.DefaultNetworkCredentials
                };
            }
            documentStore.Initialize();
            documentStore.JsonRequestFactory.RequestTimeout = TimeSpan.FromSeconds(20);

            PurgeKnownEndpointsWithTemporaryIdsThatAreDuplicate(documentStore);
        }

        static void PurgeKnownEndpointsWithTemporaryIdsThatAreDuplicate(IDocumentStore documentStore)
        {
            using (var session = documentStore.OpenSession())
            {
                var endpoints = session.Query<KnownEndpoint, KnownEndpointIndex>().ToList();

                foreach (var knownEndpoints in endpoints.GroupBy(e => $"{e.EndpointDetails.Host}{e.EndpointDetails.Name}"))
                {
                    var fixedIdsCount = knownEndpoints.Count(e => !e.HasTemporaryId);

                    //If we have knowEndpoints with non temp ids, we should delete all temp ids ones.
                    if (fixedIdsCount > 0)
                    {
                        knownEndpoints.Where(e => e.HasTemporaryId).ForEach(k => { documentStore.DatabaseCommands.Delete(documentStore.Conventions.DefaultFindFullDocumentKeyFromNonStringIdentifier(k.Id, typeof(KnownEndpoint), false), null); });
                    }
                }
            }
        }
    }
}
