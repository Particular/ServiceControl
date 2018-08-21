namespace ServiceControl.Infrastructure.RavenDB.Subscriptions
{
    using System.Threading.Tasks;

    interface IPrimableSubscriptionStorage
    {
        Task Prime();
    }
}