namespace ServiceControl.Infrastructure.DomainEvents
{
    using System.Threading.Tasks;

    public interface IStartable
    {
        Task Start(ITimeKeeper timeKeeper);
        Task Stop(ITimeKeeper timeKeeper);
    }
}