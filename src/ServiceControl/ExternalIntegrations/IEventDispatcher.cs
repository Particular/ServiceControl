namespace ServiceControl.ExternalIntegrations
{
    using System.Threading.Tasks;

    interface IEventDispatcher
    {
        Task Enqueue(object[] eventContext);
    }
}