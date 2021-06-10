namespace Particular.ServiceControl
{
    using System;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using global::ServiceControl.Infrastructure.RavenDB;
    using global::ServiceControl.LicenseManagement;
    using global::ServiceControl.Transports;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Logging;
    using NServiceBus.Serialization;
    using NServiceBus.Settings;
    using NServiceBus.Unicast.Messages;
    using Raven.Client.Embedded;
    using ServiceBus.Management.Infrastructure.Installers;
    using ServiceBus.Management.Infrastructure.Settings;

    class SetupBootstrapper
    {
        public SetupBootstrapper(Settings settings)
        {
            this.settings = settings;
        }

        public async Task Run(string username)
        {
            // Validate license:
            if (!ValidateLicense(settings))
            {
                return;
            }

            var componentSetupContext = new ComponentSetupContext();

            foreach (ServiceControlComponent component in ServiceControlMainInstance.Components)
            {
                component.Setup(settings, componentSetupContext);
            }

            if (!settings.RunInMemory) //RunInMemory is used in acceptance tests
            {
                using (var documentStore = new EmbeddableDocumentStore())
                {
                    RavenBootstrapper.Configure(documentStore, settings);
                    var service = new EmbeddedRavenDbHostedService(documentStore, new IDataMigration[0], componentSetupContext);
                    await service.StartAsync(CancellationToken.None).ConfigureAwait(false);
                    await service.StopAsync(CancellationToken.None).ConfigureAwait(false);
                }
            }
            EventSourceCreator.Create();

            if (settings.SkipQueueCreation)
            {
                log.Info("Skipping queue creation");
            }
            else
            {
                var transportSettings = MapSettings(settings);
                var transportCustomization = settings.LoadTransportCustomization();

                void Customize(EndpointConfiguration ec, TransportSettings ts)
                {
                    SetTransportSpecificFlags(ec.GetSettings(), $"{settings.ServiceName}.Errors");
                    transportCustomization.CustomizeServiceControlEndpoint(ec, ts);
                }

                await QueueCreator.CreateQueues(transportSettings, Customize, username, componentSetupContext.Queues.ToArray()).ConfigureAwait(false);
            }
        }

        bool ValidateLicense(Settings settings)
        {
            if (!string.IsNullOrWhiteSpace(settings.LicenseFileText))
            {
                if (!LicenseManager.IsLicenseValidForServiceControlInit(settings.LicenseFileText, out var errorMessageForLicenseText))
                {
                    log.Error(errorMessageForLicenseText);
                    return false;
                }

                if (!LicenseManager.TryImportLicenseFromText(settings.LicenseFileText, out var importErrorMessage))
                {
                    log.Error(importErrorMessage);
                    return false;
                }
            }
            else
            {
                var license = LicenseManager.FindLicense();
                if (!LicenseManager.IsLicenseValidForServiceControlInit(license, out var errorMessageForFoundLicense))
                {
                    log.Error(errorMessageForFoundLicense);
                    return false;
                }
            }

            return true;
        }

        static TransportSettings MapSettings(Settings settings)
        {
            var transportSettings = new TransportSettings
            {
                EndpointName = settings.ServiceName,
                ConnectionString = settings.TransportConnectionString,
                MaxConcurrency = settings.MaximumConcurrencyLevel
            };
            return transportSettings;
        }

        static void SetTransportSpecificFlags(SettingsHolder settings, string poisonQueue)
        {
            //To satisfy requirements of various transports

            //MSMQ
            settings.Set("errorQueue", poisonQueue); //Not SetDefault Because MSMQ transport verifies if that value has been explicitly set

            //RabbitMQ
            settings.SetDefault("RabbitMQ.RoutingTopologySupportsDelayedDelivery", true);

            //SQS
            settings.SetDefault("NServiceBus.AmazonSQS.DisableSubscribeBatchingOnStart", true);

            //ASB
            var builder = new ConventionsBuilder(settings);
            builder.DefiningEventsAs(type => true);
            settings.Set(builder.Conventions);

            //ASQ and ASB
            var serializer = Tuple.Create(new NewtonsoftSerializer() as SerializationDefinition, new SettingsHolder());
            settings.SetDefault("MainSerializer", serializer);

            //SQS and ASQ
            bool IsMessageType(Type t) => true;
            var ctor = typeof(MessageMetadataRegistry).GetConstructor(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(Func<Type, bool>) }, null);
#pragma warning disable CS0618 // Type or member is obsolete
            settings.SetDefault<MessageMetadataRegistry>(ctor.Invoke(new object[] { (Func<Type, bool>)IsMessageType }));
#pragma warning restore CS0618 // Type or member is obsolete
        }

        readonly Settings settings;
        static ILog log = LogManager.GetLogger<SetupBootstrapper>();
    }
}