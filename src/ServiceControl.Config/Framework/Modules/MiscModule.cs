namespace ServiceControl.Config.Framework.Modules
{
    using Autofac;
    using UI.Shell;

    public class MiscModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<RaygunFeedback>().SingleInstance();
            builder.RegisterType<LicenseStatusManager>().SingleInstance();
        }
    }
}