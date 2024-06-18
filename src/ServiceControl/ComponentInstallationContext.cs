namespace Particular.ServiceControl
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    class ComponentInstallationContext : IComponentInstallationContext
    {
        public List<string> Queues { get; } = [];

        public List<Func<IServiceProvider, Task>> InstallationTasks { get; } = [];

        public void CreateQueue(string queueName) => Queues.Add(queueName);

        public void RegisterInstallationTask(Func<IServiceProvider, Task> setupTask) => InstallationTasks.Add(setupTask);
    }
}