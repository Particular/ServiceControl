namespace Particular.ServiceControl
{
    using System.Collections.Generic;
    using System.Reflection;

    class ComponentSetupContext : IComponentSetupContext
    {
        public List<string> Queues { get; } = new List<string>();
        public List<Assembly> IndexAssemblies { get; } = new List<Assembly>();

        public void CreateQueue(string queueName)
        {
            Queues.Add(queueName);
        }

        public void AddIndexAssembly(Assembly assembly)
        {
            IndexAssemblies.Add(assembly);
        }
    }
}