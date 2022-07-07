namespace Particular.ServiceControl
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Threading.Tasks;

    class ComponentInstallationContext : IComponentInstallationContext
    {
        public List<string> Queues { get; } = new List<string>();

        public List<Assembly> IndexAssemblies { get; } = new List<Assembly>();

        public List<Func<Task>> SetupTasks { get; } = new List<Func<Task>>();

        public void CreateQueue(string queueName) => Queues.Add(queueName);

        public void AddIndexAssembly(Assembly assembly) => IndexAssemblies.Add(assembly);

        public void RegisterSetupTask(Func<Task> setupTask) => SetupTasks.Add(setupTask);
    }
}