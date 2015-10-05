using Autofac;

namespace ServiceControl.Config.Framework.Modules
{
    using Module = Module;

    public class MiscModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<RaygunFeedback>().SingleInstance();
        }
    }
}