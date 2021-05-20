namespace ServiceControl.CustomChecks
{
    using Autofac;
    using CompositeViews.Messages;

    public class CustomChecksApisModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterAssemblyTypes(ThisAssembly)
                .AssignableTo<IApi>()
                .AsSelf()
                .AsImplementedInterfaces()
                .PropertiesAutowired();
        }
    }
}