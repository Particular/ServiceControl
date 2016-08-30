﻿namespace ServiceControl.MessageFailures.Handlers
{
    using System.Collections.Generic;
    using Raven.Abstractions.Commands;
    using Raven.Abstractions.Data;
    using Raven.Json.Linq;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.Operations;

    class ImportFailedMessageHandler
    {
        static RavenJObject metadata = RavenJObject.Parse($@"
                                    {{
                                        ""Raven-Entity-Name"": ""{FailedMessage.CollectionName}"", 
                                        ""Raven-Clr-Type"": ""{typeof(FailedMessage).AssemblyQualifiedName}""
                                    }}");

        private readonly IFailedMessageEnricher[] failureEnrichers;
        private readonly IEnrichImportedMessages[] importerEnrichers;

        public ImportFailedMessageHandler(IFailedMessageEnricher[] failureEnrichers, IEnrichImportedMessages[] importerEnrichers)
        {
            this.failureEnrichers = failureEnrichers;
            this.importerEnrichers = importerEnrichers;
            
        }

        public PatchCommandData Handle(ImportFailedMessage message)
        {
            foreach (var enricher in importerEnrichers)
            {
                enricher.Enrich(message);
            }

            var documentId = FailedMessage.MakeDocumentId(message.UniqueMessageId);
            var timeOfFailure = message.FailureDetails.TimeOfFailure;
            var groups = new List<FailedMessage.FailureGroup>();

            foreach (var enricher in failureEnrichers)
            {
                groups.AddRange(enricher.Enrich(message));
            }

            return new PatchCommandData
            {
                Key = documentId,
                Patches = new[]
                {
                    new PatchRequest
                    {
                        Name = nameof(FailedMessage.Status),
                        Type = PatchCommandType.Set,
                        Value = (int) FailedMessageStatus.Unresolved
                    },
                    new PatchRequest
                    {
                        Name = nameof(FailedMessage.ProcessingAttempts),
                        Type = PatchCommandType.Add,
                        Value = RavenJToken.FromObject(new FailedMessage.ProcessingAttempt
                        {
                            AttemptedAt = timeOfFailure,
                            FailureDetails = message.FailureDetails,
                            MessageMetadata = message.Metadata,
                            MessageId = message.PhysicalMessage.MessageId,
                            Headers = message.PhysicalMessage.Headers,
                            ReplyToAddress = message.PhysicalMessage.ReplyToAddress,
                            Recoverable = message.PhysicalMessage.Recoverable,
                            CorrelationId = message.PhysicalMessage.CorrelationId,
                            MessageIntent = message.PhysicalMessage.MessageIntent
                        })
                    },
                    new PatchRequest
                    {
                        Name = nameof(FailedMessage.FailureGroups),
                        Type = PatchCommandType.Set,
                        Value = RavenJToken.FromObject(groups)
                    }
                },
                PatchesIfMissing = new[]
                {
                    new PatchRequest
                    {
                        Name = nameof(FailedMessage.UniqueMessageId),
                        Type = PatchCommandType.Set,
                        Value = message.UniqueMessageId
                    },
                    new PatchRequest
                    {
                        Name = nameof(FailedMessage.Status),
                        Type = PatchCommandType.Set,
                        Value = (int) FailedMessageStatus.Unresolved
                    },
                    new PatchRequest
                    {
                        Name = nameof(FailedMessage.ProcessingAttempts),
                        Type = PatchCommandType.Add,
                        Value = RavenJToken.FromObject(new FailedMessage.ProcessingAttempt
                        {
                            AttemptedAt = timeOfFailure,
                            FailureDetails = message.FailureDetails,
                            MessageMetadata = message.Metadata,
                            MessageId = message.PhysicalMessage.MessageId,
                            Headers = message.PhysicalMessage.Headers,
                            ReplyToAddress = message.PhysicalMessage.ReplyToAddress,
                            Recoverable = message.PhysicalMessage.Recoverable,
                            CorrelationId = message.PhysicalMessage.CorrelationId,
                            MessageIntent = message.PhysicalMessage.MessageIntent
                        })
                    },
                    new PatchRequest
                    {
                        Name = nameof(FailedMessage.FailureGroups),
                        Type = PatchCommandType.Set,
                        Value = RavenJToken.FromObject(groups)
                    }
                },
                Metadata = metadata
            };
        }
    }
}