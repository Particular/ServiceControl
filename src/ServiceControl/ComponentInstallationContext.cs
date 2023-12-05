namespace Particular.ServiceControl
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Threading.Tasks;

    class ComponentInstallationContext : IComponentInstallationContext
    {
        public List<string> Queues { get; } = [];

        public List<Assembly> IndexAssemblies { get; } = [];

        public List<Func<Task>> InstallationTasks { get; } = [];

        public void CreateQueue(string queueName) => Queues.Add(queueName);

        public void AddIndexAssembly(Assembly assembly) => IndexAssemblies.Add(assembly);

        public void RegisterInstallationTask(Func<Task> setupTask) => InstallationTasks.Add(setupTask);
    }
}