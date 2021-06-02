namespace ServiceControl.Infrastructure.RavenDB
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Raven.Client;
    using Raven.Client.Embedded;

    static class RavenHostBuilderExtensions
    {
        public static IHostBuilder UseEmbeddedRavenDb(this IHostBuilder hostBuilder,
            Func<HostBuilderContext, EmbeddableDocumentStore> documentStoreBuilder, Func<IDocumentStore, Task> storeInitializer)
        {
            hostBuilder.ConfigureServices((ctx, serviceCollection) =>
            {
                var embeddedDocumentStore = documentStoreBuilder(ctx);

                serviceCollection.AddSingleton<IDocumentStore>(embeddedDocumentStore);
                serviceCollection.AddSingleton<IHostedService>(
                    serviceProvider => new EmbeddedRavenDbHostedService(embeddedDocumentStore, storeInitializer)
                );
            });

            return hostBuilder;
        }
    }
}
