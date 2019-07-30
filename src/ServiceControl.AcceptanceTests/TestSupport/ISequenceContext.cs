namespace ServiceBus.Management.AcceptanceTests
{
    public interface ISequenceContext
    {
        int Step { get; set; }
    }
}