namespace ServiceBus.Management.Infrastructure.Nancy
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Autofac;
    using Autofac.Core;
    using Autofac.Core.Lifetime;
    using Autofac.Core.Resolving;
    using NServiceBus.Logging;
    using global::Nancy;
    using global::Nancy.Bootstrapper;
    using global::Nancy.Bootstrappers.Autofac;
    using global::Nancy.Diagnostics;
    using global::Nancy.Responses;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class NServiceBusContainerBootstrapper : AutofacNancyBootstrapper
    {
        private readonly ILifetimeScope container;

        public NServiceBusContainerBootstrapper(IContainer container)
        {
            this.container = new ILifetimeScopeWrapperNotDisposable(container);
            StaticConfiguration.EnableHeadRouting = true;
        }

        protected override NancyInternalConfiguration InternalConfiguration
        {
            get
            {
                return NancyInternalConfiguration.WithOverrides(c => c.Serializers.Remove(typeof(DefaultJsonSerializer)));
            }
        }

        protected override DiagnosticsConfiguration DiagnosticsConfiguration => new DiagnosticsConfiguration
        {
            Password = @"password"
        };

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
                //Only auto-wire modules that derive from BaseModule
                if (typeof(BaseModule).IsAssignableFrom(moduleRegistrationType.ModuleType))
                {
                    builder.RegisterType(moduleRegistrationType.ModuleType).As<INancyModule>().PropertiesAutowired();
                }
            }

            builder.Update(container.ComponentRegistry);
        }

        protected override INancyModule GetModule(ILifetimeScope container, Type moduleType)
        {
            if (typeof(BaseModule).IsAssignableFrom(moduleType))
            {
                var builder = new ContainerBuilder();

                builder.RegisterType(moduleType).As<INancyModule>().PropertiesAutowired();
                builder.Update(container.ComponentRegistry);

                return container.Resolve<INancyModule>();
            }
            return (INancyModule) container.Resolve(moduleType);
        }

        static ILog Logger = LogManager.GetLogger(typeof(NServiceBusContainerBootstrapper));
    }

    class ILifetimeScopeWrapperNotDisposable: ILifetimeScope
    {
        private readonly ILifetimeScope realScope;

        public ILifetimeScopeWrapperNotDisposable(ILifetimeScope realScope)
        {
            this.realScope = realScope;
        }

        public object ResolveComponent(IComponentRegistration registration, IEnumerable<Parameter> parameters)
        {
           return realScope.ResolveComponent(registration, parameters);
        }

        public IComponentRegistry ComponentRegistry => realScope.ComponentRegistry;

        public void Dispose()
        {
            // Not disposed on purpose, because i want NserviceBus to dispose of it.
        }

        public ILifetimeScope BeginLifetimeScope()
        {
           return  realScope.BeginLifetimeScope();
        }

        public ILifetimeScope BeginLifetimeScope(object tag)
        {
            return realScope.BeginLifetimeScope(tag);
        }

        public ILifetimeScope BeginLifetimeScope(Action<ContainerBuilder> configurationAction)
        {
            return realScope.BeginLifetimeScope(configurationAction);
        }

        public ILifetimeScope BeginLifetimeScope(object tag, Action<ContainerBuilder> configurationAction)
        {
            return realScope.BeginLifetimeScope(tag, configurationAction);
        }

        public IDisposer Disposer => realScope.Disposer;
        public object Tag => realScope.Tag;

        public event EventHandler<LifetimeScopeBeginningEventArgs> ChildLifetimeScopeBeginning
        {
            add { realScope.ChildLifetimeScopeBeginning += value; }
            remove { realScope.ChildLifetimeScopeBeginning -= value; }
        }

        public event EventHandler<LifetimeScopeEndingEventArgs> CurrentScopeEnding
        {
            add { realScope.CurrentScopeEnding += value; }
            remove { realScope.CurrentScopeEnding -= value; }
        }

        public event EventHandler<ResolveOperationBeginningEventArgs> ResolveOperationBeginning
        {
            add { realScope.ResolveOperationBeginning += value; }
            remove { realScope.ResolveOperationBeginning -= value; }
        }
    }
}