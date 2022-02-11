﻿namespace ServiceControl.Audit.Infrastructure.RavenDB
{
    using System;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Raven.Client;
    using Raven.Client.Embedded;

    static class RavenHostBuilderExtensions
    {
        public static IHostBuilder UseEmbeddedRavenDb(this IHostBuilder hostBuilder,
            Func<HostBuilderContext, EmbeddableDocumentStore> documentStoreBuilder)
        {
            hostBuilder.ConfigureServices((ctx, serviceCollection) =>
            {
                var embeddedDocumentStore = documentStoreBuilder(ctx);

                serviceCollection.AddSingleton<IDocumentStore>(embeddedDocumentStore);
                serviceCollection.AddHostedService<EmbeddedRavenDbHostedService>();
            });

            return hostBuilder;
        }
    }
}