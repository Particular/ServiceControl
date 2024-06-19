namespace Particular.ServiceControl
{
    interface IComponentInstallationContext
    {
        void CreateQueue(string queueName);
    }
}