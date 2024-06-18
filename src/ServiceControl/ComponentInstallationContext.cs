namespace Particular.ServiceControl
{
    using System.Collections.Generic;

    class ComponentInstallationContext : IComponentInstallationContext
    {
        public List<string> Queues { get; } = [];

        public void CreateQueue(string queueName) => Queues.Add(queueName);
    }
}