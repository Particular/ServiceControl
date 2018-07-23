namespace ServiceControl.LoadTests.AuditGenerator
{
    using System.Threading.Tasks;
    using Messages;
    using NServiceBus;

    class QueueLengthMessageHandler : IHandleMessages<QueueLengthReport>
    {
        public QueueLengthMessageHandler(LoadGenerators generators)
        {
            this.generators = generators;
        }

        public Task Handle(QueueLengthReport message, IMessageHandlerContext context)
        {
            return generators.QueueLengthReported(message.Queue, message.Machine, message.Length);
        }

        LoadGenerators generators;
    }
}