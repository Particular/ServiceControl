namespace ServiceControl.Infrastructure.RavenDB
{
    using System;
    using System.Globalization;
    using System.IO;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Pipeline;
    using NServiceBus.RavenDB;
    using Raven.Client;
    using Raven.Client.Embedded;
    using Raven.Client.Indexes;
    using ServiceBus.Management.Infrastructure.Settings;
    using INeedInitialization = NServiceBus.INeedInitialization;

    public class RavenBootstrapper : INeedInitialization
    {
        public void Init()
        {
            Directory.CreateDirectory(Settings.DbPath);

            var documentStore = new EmbeddableDocumentStore
            {
                DataDirectory = Settings.DbPath,
                UseEmbeddedHttpServer = Settings.ExposeRavenDB,
                EnlistInDistributedTransactions = false,
            };

            documentStore.Configuration.Settings.Add("Raven/ActiveBundles", "DocumentExpiration"); // Enable the expiration bundle
            documentStore.Configuration.Settings.Add("Raven/Expiration/DeleteFrequencySeconds", Settings.ExpirationProcessTimerInSeconds.ToString(CultureInfo.InvariantCulture)); // Run the deletion operation every 24 hours

            if (Settings.Schema.Equals("https", StringComparison.InvariantCultureIgnoreCase))
            {
                documentStore.Configuration.UseSsl = true;
            }
            documentStore.Configuration.Port = Settings.Port;
            documentStore.Configuration.HostName = (Settings.Hostname == "*" || Settings.Hostname == "+")
                ? "localhost"
                : Settings.Hostname;
            documentStore.Configuration.CompiledIndexCacheDirectory = Settings.DbPath;
            documentStore.Configuration.VirtualDirectory = Settings.VirtualDirectory + "/storage";

            documentStore.Conventions.SaveEnumsAsIntegers = true;

            documentStore.Initialize();

            Logger.Info("Index creation started");

            if (Settings.CreateIndexSync)
            {
                IndexCreation.CreateIndexes(typeof(RavenBootstrapper).Assembly, documentStore);
            }
            else
            {
                IndexCreation.CreateIndexesAsync(typeof(RavenBootstrapper).Assembly, documentStore)
                    .ContinueWith(c =>
                    {
                        if (c.IsFaulted)
                        {
                            Logger.Error("Index creation failed", c.Exception);
                        }
                    });
            }

            Configure.Instance.Configurer.RegisterSingleton<IDocumentStore>(documentStore);
            Configure.Component(builder =>
            {
#pragma warning disable 618
                var context = builder.Build<PipelineExecutor>().CurrentContext;
#pragma warning restore 618

                IDocumentSession session;

                if (context.TryGet(out session))
                {
                    return session;
                }

                throw new InvalidOperationException("No session available");
            }, DependencyLifecycle.InstancePerCall);

            Configure.Instance.RavenDBStorageWithSelfManagedSession(documentStore, false,
                () => Configure.Instance.Builder.Build<IDocumentSession>())
                .UseRavenDBSagaStorage()
                .UseRavenDBSubscriptionStorage()
                .UseRavenDBTimeoutStorage();
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(RavenBootstrapper));
    }
}
