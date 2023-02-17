namespace ServiceControl.Transports
{
    using System.Threading.Tasks;

    public interface IQueueIngestor
    {
        Task Start();
        Task Stop();
    }
}