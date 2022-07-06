namespace Particular.ServiceControl
{
    using System;
    using System.Reflection;
    using System.Threading.Tasks;

    interface IComponentSetupContext
    {
        void CreateQueue(string queueName);
        void AddIndexAssembly(Assembly assembly);
        void RegisterSetupTask(Func<Task> setupTask);
    }
}