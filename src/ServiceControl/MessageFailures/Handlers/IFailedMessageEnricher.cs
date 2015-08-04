namespace ServiceControl.MessageFailures.Handlers
{
    using ServiceControl.Contracts.Operations;

    public interface IFailedMessageEnricher
    {
        void Enrich(FailedMessage message, ImportFailedMessage source);
    }
}