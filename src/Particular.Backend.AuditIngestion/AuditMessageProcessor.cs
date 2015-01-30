namespace ServiceControl.MessageTypes
{
    using System.Collections.Generic;
    using Particular.Backend.AuditIngestion.Api;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.Shell.Api.Ingestion;

    public class AuditMessageProcessor : IProcessIngestedMessages
    {
        readonly EndpointDetailsParser endpointDetailsParser;
        readonly MessageTypeParser messageTypeParser;
        readonly IdGenerator idGenerator;
        readonly IEnumerable<IProcessAuditMessages> auditProcessors; 

        public AuditMessageProcessor(EndpointDetailsParser endpointDetailsParser, MessageTypeParser messageTypeParser, IdGenerator idGenerator, IEnumerable<IProcessAuditMessages> auditProcessors)
        {
            this.endpointDetailsParser = endpointDetailsParser;
            this.messageTypeParser = messageTypeParser;
            this.auditProcessors = auditProcessors;
            this.idGenerator = idGenerator;
        }

        public void Process(IngestedMessage message)
        {
            var headers = message.Headers;

            var auditMessage = new IngestedAuditMessage(
                idGenerator.ParseId(headers),
                idGenerator.GenerateUniqueId(headers),
                message.Body, 
                headers, 
                messageTypeParser.Parse(headers),
                endpointDetailsParser.ParseSendingEndpoint(headers),
                endpointDetailsParser.ParseProcessingEndpoint(headers));

            foreach (var processor in auditProcessors)
            {
                processor.Process(auditMessage);
            }
        }
    }
}