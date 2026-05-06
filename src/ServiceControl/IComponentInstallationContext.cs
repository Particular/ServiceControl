namespace Particular.ServiceControl
{
    public interface IComponentInstallationContext
    {
        void CreateQueue(string queueName);

        void AddEventPublished<TEvent>();
    }
}