namespace ServiceControl.Infrastructure.DomainEvents
{
    public interface IProvideStartable
    {
        IStartable Startable { get; }
    }
}