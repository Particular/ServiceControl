namespace ServiceControl.MessageFailures.Api
{
    using System.Reflection;
    using Autofac;
    using CompositeViews.Messages;

    public class ApisModule : Autofac.Module
    {
        readonly Assembly assembly;

        public ApisModule(Assembly assembly = null) => this.assembly = assembly;

        protected override void Load(ContainerBuilder builder) =>
            builder.RegisterAssemblyTypes(assembly ?? ThisAssembly)
                .AssignableTo<IApi>()
                .AsSelf()
                .AsImplementedInterfaces()
                .PropertiesAutowired();
    }
}