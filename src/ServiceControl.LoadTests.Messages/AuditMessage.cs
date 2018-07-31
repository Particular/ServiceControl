namespace ServiceControl.LoadTests.Messages
{
    using NServiceBus;

    public class AuditMessage : IMessage
    {
        public byte[] Data { get; set; }
    }
}