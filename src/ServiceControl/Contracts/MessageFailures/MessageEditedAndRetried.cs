namespace ServiceControl.Contracts.MessageFailures
{
    using Infrastructure.DomainEvents;

    public class MessageEditedAndRetried : IDomainEvent
    {
        public string FailedMessageId { get; set; }
        public string RetriedMessageId { get; set; }
        public string EditId { get; set; }
    }
}

//TODO: Move this to ServiceControl.Contracts package
namespace ServiceControl.Contracts
{
    public class MessageEditedAndRetried
    {
        public string FailedMessageId { get; set; }
        public string RetriedMessageId { get; set; }
        public string EditId { get; set; }
    }
}