namespace Particular.ServiceControl
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;

    class ComponentSetupContext : IComponentSetupContext
    {
        Action<string> queueCreationRequestedCallback;
        public List<string> Queues { get; } = new List<string>();

        public List<Assembly> IndexAssemblies { get; } = new List<Assembly>();

        public void OnQueueCreationRequested(Action<string> callback)
        {
            if (queueCreationRequestedCallback != null)
            {
                throw new Exception("Only one callback can be registered");
            }

            queueCreationRequestedCallback = callback;
            foreach (string queue in Queues)
            {
                queueCreationRequestedCallback(queue);
            }
            Queues.Clear();
        }

        public void CreateQueue(string queueName)
        {
            if (queueCreationRequestedCallback == null)
            {
                Queues.Add(queueName);
            }
            else
            {
                queueCreationRequestedCallback(queueName);
            }
        }

        public void AddIndexAssembly(Assembly assembly)
        {
            IndexAssemblies.Add(assembly);
        }
    }
}