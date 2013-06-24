namespace ServiceBus.Management.Api.Nancy
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus;
    using NServiceBus.ObjectBuilder.Common;
    using global::Nancy;
    using global::Nancy.Bootstrapper;
    using global::Nancy.Diagnostics;
    using global::Nancy.Responses;

    public class NServiceBusContainerBootstrapper : NancyBootstrapperWithRequestContainerBase<IContainer>
    {
        protected override void ApplicationStartup(IContainer container, IPipelines pipelines)
        {
            StaticConfiguration.EnableRequestTracing = true;
        }

        protected override NancyInternalConfiguration InternalConfiguration
        {
            get
            {
                // Insert at position 0 so it takes precedence over the built in one.
                return NancyInternalConfiguration.WithOverrides(
                        c => c.Serializers.Remove(typeof (DefaultJsonSerializer)));
            }
        }

        protected override DiagnosticsConfiguration DiagnosticsConfiguration
        {
            get { return new DiagnosticsConfiguration { Password = @"password" }; }
        }

        protected override IDiagnostics GetDiagnostics()
        {
            return ApplicationContainer.Build(typeof(IDiagnostics)) as IDiagnostics;
        }

        protected override IEnumerable<IApplicationStartup> GetApplicationStartupTasks()
        {
            return ApplicationContainer.BuildAll(typeof(IApplicationStartup)) as IEnumerable<IApplicationStartup>;
        }

        protected override IEnumerable<IApplicationRegistrations> GetApplicationRegistrationTasks()
        {
            return
                ApplicationContainer.BuildAll(typeof(IApplicationRegistrations)) as
                IEnumerable<IApplicationRegistrations>;
        }

        protected override INancyEngine GetEngineInternal()
        {
            return ApplicationContainer.Build(typeof(INancyEngine)) as INancyEngine;
        }

        protected override IContainer GetApplicationContainer()
        {
            var builder = Configure.Instance.Builder as CommonObjectBuilder;
            if (builder == null)
                throw new ApplicationException(@"Builder is not configured as the common object builder. 
                Cannot use the NServiceBus container bootstrapper for Nancy");
            return builder.Container;
        }

        protected override void RegisterBootstrapperTypes(IContainer container)
        {
            container.RegisterSingleton(typeof(INancyModuleCatalog), this);
        }

        protected override void RegisterTypes(IContainer container, IEnumerable<TypeRegistration> typeRegistrations)
        {
            foreach (var typeRegistration in typeRegistrations)
            {
                container.Configure(typeRegistration.ImplementationType, DependencyLifecycle.SingleInstance);
            }
        }

        protected override void RegisterCollectionTypes(IContainer container,
                                                        IEnumerable<CollectionTypeRegistration>
                                                            collectionTypeRegistrations)
        {
            foreach (var implementationType in collectionTypeRegistrations.SelectMany(collectionTypeRegistration => collectionTypeRegistration.ImplementationTypes))
            {
                container.Configure(implementationType, DependencyLifecycle.SingleInstance);
            }
        }

        protected override void RegisterInstances(IContainer container,
                                                  IEnumerable<InstanceRegistration> instanceRegistrations)
        {
            foreach (var instanceRegistration in instanceRegistrations)
            {
                container.RegisterSingleton(instanceRegistration.RegistrationType, instanceRegistration.Implementation);
            }
        }

        protected override IContainer CreateRequestContainer()
        {
            return GetApplicationContainer().BuildChildContainer();
        }

        protected override void RegisterRequestContainerModules(IContainer container, IEnumerable<ModuleRegistration> moduleRegistrationTypes)
        {
            foreach (var moduleRegistrationType in moduleRegistrationTypes)
            {
                Configure.Instance.Configurer.ConfigureComponent(moduleRegistrationType.ModuleType,
                                                                 DependencyLifecycle.SingleInstance);
                container.RegisterSingleton(typeof(NancyModule),
                                            Configure.Instance.Builder.Build(moduleRegistrationType.ModuleType));
            }
        }

        protected override IEnumerable<INancyModule> GetAllModules(IContainer container)
        {
            return container.BuildAll(typeof(NancyModule)).Cast<INancyModule>();
        }

        protected override INancyModule GetModule(IContainer container, Type moduleType)
        {
            return container.Build(moduleType) as INancyModule;
        }
    }
}