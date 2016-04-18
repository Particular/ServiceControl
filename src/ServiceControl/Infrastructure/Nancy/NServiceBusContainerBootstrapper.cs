namespace ServiceBus.Management.Infrastructure.Nancy
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Autofac;
    using NServiceBus.Logging;
    using global::Nancy;
    using global::Nancy.Bootstrapper;
    using global::Nancy.Bootstrappers.Autofac;
    using global::Nancy.Diagnostics;
    using global::Nancy.Responses;

    public class NServiceBusContainerBootstrapper : AutofacNancyBootstrapper
    {
        private readonly IContainer container;

        public NServiceBusContainerBootstrapper(IContainer container)
        {
            StaticConfiguration.EnableHeadRouting = true;
            this.container = container;
        }

        protected override NancyInternalConfiguration InternalConfiguration
        {
            get
            {
                return NancyInternalConfiguration.WithOverrides(c => c.Serializers.Remove(typeof(DefaultJsonSerializer)));
            }
        }

        protected override DiagnosticsConfiguration DiagnosticsConfiguration
        {
            get
            {
                return new DiagnosticsConfiguration
                {
                    Password = @"password"
                };
            }
        }

        protected override void ApplicationStartup(ILifetimeScope container, IPipelines pipelines)
        {
            pipelines.AfterRequest.AddItemToStartOfPipeline(new PipelineItem<Action<NancyContext>>("NotModified",
                NotModifiedStatusExtension.Check));
            pipelines.AfterRequest.InsertAfter("NotModified",
                new PipelineItem<Action<NancyContext>>("CacheControl", ExtraHeaders.Add));
            pipelines.AfterRequest.InsertAfter("NotModified",
                new PipelineItem<Action<NancyContext>>("Version", VersionExtension.Add));
            pipelines.AfterRequest.AddItemToEndOfPipeline(new PipelineItem<Action<NancyContext>>("Compression",
                NancyCompressionExtension.CheckForCompression));

            pipelines.OnError.AddItemToEndOfPipeline((c, ex) =>
            {
                var aggregateEx = ex as AggregateException;

                //this is a workaround for the nlog issue https://github.com/Particular/NServiceBus/issues/1842 
                if (aggregateEx != null)
                {
                    var builder = new StringBuilder();

                    foreach (var innerEx in aggregateEx.InnerExceptions)
                    {
                        builder.AppendLine(innerEx.ToString());
                    }

                    Logger.Error("Http call failed: " + builder.ToString());
                }
                else
                {
                    Logger.Error("Http call failed", ex);
                }
               

                return c.Response;
            });
        }

        protected override ILifetimeScope GetApplicationContainer()
        {
            return container;
        }

        protected override void RegisterRequestContainerModules(ILifetimeScope container,
            IEnumerable<ModuleRegistration> moduleRegistrationTypes)
        {
            var builder = new ContainerBuilder();

            foreach (var moduleRegistrationType in moduleRegistrationTypes)
            {
                builder.RegisterType(moduleRegistrationType.ModuleType).As<INancyModule>().PropertiesAutowired();
            }

            builder.Update(container.ComponentRegistry);
        }

        protected override INancyModule GetModule(ILifetimeScope container, Type moduleType)
        {
            var builder = new ContainerBuilder();

            builder.RegisterType(moduleType).As<INancyModule>().PropertiesAutowired();
            builder.Update(container.ComponentRegistry);

            return container.Resolve<INancyModule>();
        }

        static ILog Logger = LogManager.GetLogger(typeof(NServiceBusContainerBootstrapper));
    }
}
