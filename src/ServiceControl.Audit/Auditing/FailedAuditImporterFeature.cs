namespace ServiceControl.Audit.Auditing
{
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Logging;
    using Particular.ServiceControl.Audit;
    using Raven.Client;

    class FailedAuditImporterFeature : Feature
    {
        public FailedAuditImporterFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<ImportFailedAudits>(DependencyLifecycle.SingleInstance);

            context.RegisterStartupTask(b => new DetectFailedImportsOnStartup(context.Settings.Get<InstanceStartUpDetails>(), b.Build<IDocumentStore>()));
        }

        class DetectFailedImportsOnStartup : FeatureStartupTask
        {
            public DetectFailedImportsOnStartup(InstanceStartUpDetails startUpDetails, IDocumentStore store)
            {
                this.startUpDetails = startUpDetails;
                this.store = store;
            }

            protected async override Task OnStart(IMessageSession session)
            {
                using (var documentSession = store.OpenAsyncSession())
                {
                    var query = documentSession.Query<FailedAuditImport, FailedAuditImportIndex>();
                    using (var ie = await documentSession.Advanced.StreamAsync(query)
                        .ConfigureAwait(false))
                    {
                        var hasFailedImports = await ie.MoveNextAsync().ConfigureAwait(false);
                        if (hasFailedImports)
                        {
                            var message = @"One or more audit messages have failed to import properly into ServiceControl and have been stored in the ServiceControl database.
The import of these messages could have failed for a number of reasons and ServiceControl is not able to automatically reimport them. For guidance on how to resolve this see https://docs.particular.net/servicecontrol/import-failed-audit";

                            Logger.Warn(message);
                        }

                        startUpDetails.Details["audit.has-failed-imports"] = hasFailedImports.ToString();
                    }
                }


            }

            protected override Task OnStop(IMessageSession session)
            {
                return Task.CompletedTask;
            }

            readonly InstanceStartUpDetails startUpDetails;
            readonly IDocumentStore store;
            static readonly ILog Logger = LogManager.GetLogger(typeof(DetectFailedImportsOnStartup));
        }
    }
}