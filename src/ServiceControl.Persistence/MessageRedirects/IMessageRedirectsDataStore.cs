namespace ServiceControl.Persistence.MessageRedirects
{
    using System.Threading.Tasks;

    public interface IMessageRedirectsDataStore
    {
        Task<MessageRedirectsCollection> GetOrCreate();
        Task Save(MessageRedirectsCollection redirects);
    }
}