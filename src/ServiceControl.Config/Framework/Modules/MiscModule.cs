namespace ServiceControl.Config.Framework.Modules
{
    using Autofac;

    public class MiscModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<RaygunFeedback>().SingleInstance();
        }
    }
}