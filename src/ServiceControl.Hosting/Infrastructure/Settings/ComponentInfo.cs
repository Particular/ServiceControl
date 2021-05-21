namespace ServiceBus.Management.Infrastructure.Settings
{
    using System.Reflection;

    public class ComponentInfo
    {
        public ComponentInfo(Assembly assembly) => Assembly = assembly;

        public Assembly Assembly { get; }
    }
}