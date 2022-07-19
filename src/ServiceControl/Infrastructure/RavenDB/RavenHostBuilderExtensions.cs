namespace ServiceControl.Infrastructure.RavenDB
{
    using CustomChecks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Raven.Client;
    using Raven.Client.Embedded;

    static class RavenHostBuilderExtensions
    {
        public static IHostBuilder UseEmbeddedRavenDb(this IHostBuilder hostBuilder, EmbeddableDocumentStore documentStore)
        {
            hostBuilder.ConfigureServices((ctx, serviceCollection) =>
            {
                serviceCollection.AddSingleton<IDocumentStore>(documentStore);
                serviceCollection.AddHostedService<EmbeddedRavenDbHostedService>();

                serviceCollection.AddCustomCheck<CheckRavenDBIndexErrors>();
                serviceCollection.AddCustomCheck<CheckRavenDBIndexLag>();
            });

            return hostBuilder;
        }
    }
}
