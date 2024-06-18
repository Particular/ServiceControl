namespace Particular.ServiceControl
{
    using System;
    using System.Threading.Tasks;

    interface IComponentInstallationContext
    {
        void CreateQueue(string queueName);
        void RegisterInstallationTask(Func<IServiceProvider, Task> setupTask);
    }
}