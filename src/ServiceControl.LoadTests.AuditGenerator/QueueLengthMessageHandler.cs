namespace ServiceControl.LoadTests.AuditGenerator
{
    using System.Threading.Tasks;
    using NServiceBus;
    using Messages;

    class QueueLengthMessageHandler : IHandleMessages<QueueLengthReport>
    {
        LoadGenerators generators;

        public QueueLengthMessageHandler(LoadGenerators generators)
        {
            this.generators = generators;
        }

        public Task Handle(QueueLengthReport message, IMessageHandlerContext context)
        {
            return generators.QueueLengthReported(message.Queue, message.Machine, message.Length);
        }
    }
}