namespace ServiceControl.LoadTests.AuditGenerator
{
    using System.Threading.Tasks;
    using Messages;
    using NServiceBus;

    class ProcessingReportHandler : IHandleMessages<ProcessingReport>
    {
        public ProcessingReportHandler(LoadGenerators generators)
        {
            this.generators = generators;
        }

        public Task Handle(ProcessingReport message, IMessageHandlerContext context)
        {
            var processedAudits = message.HostId == Program.HostId
                ? message.Audits
                : long.MinValue; //We ignore reports for different hosts

            return generators.ProcessedCountReported(message.AuditQueue, processedAudits);
        }

        LoadGenerators generators;
    }
}