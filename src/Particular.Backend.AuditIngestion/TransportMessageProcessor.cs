namespace ServiceControl.MessageTypes
{
    using System.Collections.Generic;
    using NServiceBus;
    using Particular.Operations.Ingestion.Api;
    using ServiceControl.Contracts.Operations;

    public class TransportMessageProcessor
    {
        readonly EndpointInstanceParser endpointInstanceParser;
        readonly MessageTypeParser messageTypeParser;
        readonly IdGenerator idGenerator;
        readonly IEnumerable<IProcessSuccessfulMessages> successfulMessageProcessors; 
        readonly IEnumerable<IProcessFailedMessages> failedMessageProcessors; 

        public TransportMessageProcessor(EndpointInstanceParser endpointInstanceParser, MessageTypeParser messageTypeParser, IdGenerator idGenerator, 
            IEnumerable<IProcessSuccessfulMessages> successfulMessageProcessors,
            IEnumerable<IProcessFailedMessages> failedMessageProcessors)
        {
            this.endpointInstanceParser = endpointInstanceParser;
            this.messageTypeParser = messageTypeParser;
            this.idGenerator = idGenerator;
            this.successfulMessageProcessors = successfulMessageProcessors;
            this.failedMessageProcessors = failedMessageProcessors;
        }

        public void ProcessSuccessful(TransportMessage transportMessage)
        {
            var message = ConvertMessage(transportMessage);

            foreach (var processor in successfulMessageProcessors)
            {
                processor.ProcessSuccessful(message);
            }
        }
        
        public void ProcessFailed(TransportMessage transportMessage)
        {
            var message = ConvertMessage(transportMessage);

            foreach (var processor in failedMessageProcessors)
            {
                processor.ProcessFailed(message);
            }
        }

        IngestedMessage ConvertMessage(TransportMessage ingestedMessage)
        {
            var headers = new HeaderCollection(ingestedMessage.Headers);
            var auditMessage = new IngestedMessage(
                idGenerator.ParseId(headers),
                idGenerator.GenerateUniqueId(headers),
                ingestedMessage.Recoverable,
                ingestedMessage.Body,
                headers,
                messageTypeParser.Parse(headers),
                endpointInstanceParser.ParseSendingEndpoint(headers),
                endpointInstanceParser.ParseProcessingEndpoint(headers));
            return auditMessage;
        }
    }
}