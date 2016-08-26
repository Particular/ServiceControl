﻿namespace ServiceControl.Infrastructure.RavenDB
{
    using System;
    using System.Linq;
    using System.Net;
    using NServiceBus;
    using NServiceBus.Configuration.AdvanceExtensibility;
    using NServiceBus.Persistence;
    using NServiceBus.Pipeline;
    using Raven.Abstractions.Extensions;
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

            configuration.RegisterComponents(c => 
                c.ConfigureComponent(builder =>
                {
                    var context = builder.Build<PipelineExecutor>().CurrentContext;

                    IDocumentSession session;

                    if (context.TryGet(out session))
                    {
                        return session;
                    }

                    throw new InvalidOperationException("No session available");
                }, DependencyLifecycle.InstancePerCall));

            configuration.UsePersistence<CachedRavenDBPersistence, StorageType.Subscriptions>();

            configuration.Pipeline.Register<RavenRegisterStep>();
        }

        void StartRaven(DocumentStore documentStore, Settings settings)
        {
            documentStore.Url = settings.StorageUrl;
            documentStore.DefaultDatabase = "ServiceControl";
            documentStore.EnlistInDistributedTransactions = false;
            documentStore.Conventions.SaveEnumsAsIntegers = true;
            documentStore.Credentials = CredentialCache.DefaultNetworkCredentials;
            documentStore.Initialize();

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
