﻿namespace ServiceControl.Config.Framework.Modules
{
    using System.Linq;
    using Autofac;
    using Caliburn.Micro;
    using Config.Commands;

    public class CaliburnModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<ServiceControlWindowManager>().As<IWindowManager>().As<IServiceControlWindowManager>().InstancePerLifetimeScope();
            builder.RegisterType<EventAggregator>().As<IEventAggregator>().InstancePerLifetimeScope();

            // register view models
            builder.RegisterAssemblyTypes(AssemblySource.Instance.ToArray())
                .Where(type => type.Namespace != null && type.Namespace.StartsWith("ServiceControl.Config.UI.") && type.Name.EndsWith("ViewModel"))
                .AsSelf()
                .InstancePerDependency();

            // register views
            builder.RegisterAssemblyTypes(AssemblySource.Instance.ToArray())
                .Where(type => type.Namespace != null && type.Namespace.StartsWith("ServiceControl.Config.UI.") && type.Name.EndsWith("View"))
                .AsSelf()
                .InstancePerDependency();

            // register commands
            builder.RegisterAssemblyTypes(AssemblySource.Instance.ToArray())
                .Where(type => type.Namespace != null && type.Namespace.Equals("ServiceControl.Config.Commands") && type.Name.EndsWith("Command"))
                .AsSelf()
                .InstancePerDependency();

            // Generics are not auto registered
            builder.RegisterGeneric(typeof(OpenViewModelCommand<>))
                .AsSelf()
                .InstancePerDependency();
        }
    }
}