namespace ServiceBus.Management.Infrastructure.Settings
{
    using System.Reflection;
    using Module = Autofac.Module;

    public class ComponentInfo
    {
        public ComponentInfo(Assembly assembly, Module apiModule)
        {
            Assembly = assembly;
            ApiModule = apiModule;
        }

        public Assembly Assembly { get; }
        public Module ApiModule { get; }
    }
}