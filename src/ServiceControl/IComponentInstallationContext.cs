namespace Particular.ServiceControl
{
    using System;
    using System.Reflection;
    using System.Threading.Tasks;

    interface IComponentInstallationContext
    {
        void CreateQueue(string queueName);
        void AddIndexAssembly(Assembly assembly);
        void RegisterInstallationTask(Func<Task> setupTask);
    }
}