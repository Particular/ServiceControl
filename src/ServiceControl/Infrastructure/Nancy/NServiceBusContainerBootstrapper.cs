namespace ServiceControl.Infrastructure.Nancy
{
    using System;
    using System.Collections.Generic;
    using Autofac;
    using global::Nancy;
    using global::Nancy.Bootstrapper;
    using global::Nancy.Bootstrappers.Autofac;
    using global::Nancy.Diagnostics;
    using global::Nancy.Responses;
    using Particular.ServiceControl;
   
    public class NServiceBusContainerBootstrapper : AutofacNancyBootstrapper
    {
        protected override NancyInternalConfiguration InternalConfiguration
        {
            get
            {
                return NancyInternalConfiguration.WithOverrides(c => c.Serializers.Remove(typeof(DefaultJsonSerializer)));
            }
        }

        protected override DiagnosticsConfiguration DiagnosticsConfiguration
        {
            get { return new DiagnosticsConfiguration {Password = @"password"}; }
        }

        protected override void ApplicationStartup(ILifetimeScope container, IPipelines pipelines)
        {
            pipelines.AfterRequest.AddItemToStartOfPipeline(new PipelineItem<Action<NancyContext>>("NotModified",
                NotModifiedStatusExtension.Check));
            pipelines.AfterRequest.InsertAfter("NotModified",
                new PipelineItem<Action<NancyContext>>("CacheControl", CacheControlExtension.Add));
            pipelines.AfterRequest.InsertAfter("NotModified",
                new PipelineItem<Action<NancyContext>>("Version", VersionExtension.Add));
            pipelines.AfterRequest.AddItemToEndOfPipeline(new PipelineItem<Action<NancyContext>>("Compression",
                NancyCompressionExtension.CheckForCompression));
        }

        protected override ILifetimeScope GetApplicationContainer()
        {
            return EndpointConfig.Container;
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
    }
}