namespace Particular.ServiceControl
{
    using System.Reflection;

    interface IComponentSetupContext
    {
        void CreateQueue(string queueName);
        void AddIndexAssembly(Assembly assembly);
    }
}