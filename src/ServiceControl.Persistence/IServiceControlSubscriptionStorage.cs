namespace ServiceControl.Persistence
{
    using System.Threading.Tasks;
    using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

    public interface IServiceControlSubscriptionStorage : ISubscriptionStorage
    {
        Task Initialize();
    }
}