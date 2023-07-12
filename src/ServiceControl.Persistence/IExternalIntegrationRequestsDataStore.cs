namespace ServiceControl.Persistence
{
    using System;
    using System.Threading.Tasks;

    public interface IExternalIntegrationRequestsDataStore
    {
        void Subscribe(Func<object[], Task> callback);
        Task Stop();
    }
}