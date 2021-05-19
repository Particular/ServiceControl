namespace ServiceControl.MessageFailures.Api
{
    using Autofac;
    using CompositeViews.Messages;

    public class HeartbeatsApisModule : Module
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