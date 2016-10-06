namespace ServiceControl.Operations.Error
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using NServiceBus;
    using Raven.Abstractions.Commands;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Extensions;
    using Raven.Imports.Newtonsoft.Json;
    using Raven.Json.Linq;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Handlers;
    using ServiceControl.Operations;
    using ServiceControl.Operations.BodyStorage;
    using FailedMessage = ServiceControl.MessageFailures.FailedMessage;
    using JsonSerializer = Raven.Imports.Newtonsoft.Json.JsonSerializer;
    using DictionaryExtensions = ServiceControl.Infrastructure.DictionaryExtensions;

    class PatchCommandDataFactory
    {
        private const string SEPARATOR = " ";
        private static RavenJObject jObjectMetadata;
        private static JsonSerializer serializer;

        private readonly IFailedMessageEnricher[] failureEnrichers;
        private readonly IEnrichImportedMessages[] importerEnrichers;
        private readonly IMessageBodyStoragePolicy bodyStoragePolicy;
        private readonly IMessageBodyStore messageBodyStore;

        static PatchCommandDataFactory()
        {
            serializer = JsonExtensions.CreateDefaultJsonSerializer();
            serializer.TypeNameHandling = TypeNameHandling.Auto;

            jObjectMetadata = RavenJObject.Parse($@"
                                    {{
                                        ""Raven-Entity-Name"": ""{FailedMessage.CollectionName}"",
                                        ""Raven-Clr-Type"": ""{typeof(FailedMessage).AssemblyQualifiedName}""
                                    }}");
        }

        public PatchCommandDataFactory(IFailedMessageEnricher[] failureEnrichers, IEnrichImportedMessages[] importerEnrichers, IMessageBodyStoragePolicy bodyStoragePolicy, IMessageBodyStore messageBodyStore)
        {
            this.failureEnrichers = failureEnrichers;
            this.importerEnrichers = importerEnrichers;
            this.bodyStoragePolicy = bodyStoragePolicy;
            this.messageBodyStore = messageBodyStore;
        }

        private static void WriteMetadata(IDictionary<string, object> messageMeta, MessageBodyMetadata bodyMeta)
        {
            messageMeta.Add("ContentLength", bodyMeta.Size);
            messageMeta.Add("ContentType", bodyMeta.ContentType);
            messageMeta.Add("BodyUrl", $"/messages/{bodyMeta.MessageId}/body_v2");
        }

        void AddBodyDetails(Dictionary<string, object> metadata, ClaimsCheck bodyStorageClaimsCheck)
        {
            WriteMetadata(metadata, bodyStorageClaimsCheck.Metadata);

            if (!bodyStorageClaimsCheck.Stored)
            {
                metadata.Add("BodyNotStored", true);
            }
            //else if (bodyStoragePolicy.ShouldIndex(bodyStorageClaimsCheck.Metadata))
            //{
            //    byte[] messageBody;
            //    MessageBodyMetadata _;

            //    if (messageBodyStore.TryGet(BodyStorageTags.ErrorPersistent, bodyStorageClaimsCheck.Metadata.MessageId, out messageBody, out _))
            //    {
            //        metadata.Add("Body", Encoding.UTF8.GetString(messageBody));
            //    }
            //}
        }

        public FailedMessage New(string uniqueId, Dictionary<string, string> headers, bool recoverable, ClaimsCheck bodyStorageClaimsCheck, FailureDetails failureDetails)
        {
            var groups = new List<FailedMessage.FailureGroup>();

            foreach (var enricher in failureEnrichers)
            {
                groups.AddRange(enricher.Enrich(failureDetails));
            }

            var metadata = new Dictionary<string, object>();

            DictionaryExtensions.CheckIfKeyExists(Headers.MessageId, headers, messageId => metadata.Add("MessageId", messageId));

            // NOTE: Pulled out of the TransportMessage class
            var intent = (MessageIntentEnum)0;
            string str;
            if (headers.TryGetValue("NServiceBus.MessageIntent", out str))
            {
                Enum.TryParse(str, true, out intent);
            }
            metadata.Add("MessageIntent", intent);
            metadata.Add("HeadersForSearching", string.Join(SEPARATOR, headers.Values));

            foreach (var enricher in importerEnrichers)
            {
                enricher.Enrich(headers, metadata);
            }

            var timeOfFailure = failureDetails.TimeOfFailure;
            string correlationId;
            headers.TryGetValue(Headers.CorrelationId, out correlationId);

            string replyToAddress;
            headers.TryGetValue(Headers.ReplyToAddress, out replyToAddress);

            AddBodyDetails(metadata, bodyStorageClaimsCheck);

            return new FailedMessage
            {
                Id = $"FailedMessages/{uniqueId}",
                Status = FailedMessageStatus.Unresolved,
                UniqueMessageId = uniqueId,
                FailureGroups = groups,
                ProcessingAttempts =
                {
                    new FailedMessage.ProcessingAttempt
                    {
                        AttemptedAt = timeOfFailure,
                        FailureDetails = failureDetails,
                        MessageMetadata = metadata,
                        MessageId = headers[Headers.MessageId],
                        Headers = headers,
                        ReplyToAddress = replyToAddress,
                        Recoverable = recoverable,
                        CorrelationId = correlationId,
                        MessageIntent = intent
                    }
                }
            };
        }

        public PatchCommandData Patch(string uniqueId, Dictionary<string, string> headers, bool recoverable, ClaimsCheck bodyStorageClaimsCheck, FailureDetails failureDetails)
        {
            var metadata = new Dictionary<string, object>();

            DictionaryExtensions.CheckIfKeyExists(Headers.MessageId, headers, messageId => metadata.Add("MessageId", messageId));

            // NOTE: Pulled out of the TransportMessage class
            var intent = (MessageIntentEnum)0;
            string str;
            if (headers.TryGetValue("NServiceBus.MessageIntent", out str))
            {
                Enum.TryParse(str, true, out intent);
            }
            metadata.Add("MessageIntent", intent);
            metadata.Add("HeadersForSearching", string.Join(SEPARATOR, headers.Values));

            foreach (var enricher in importerEnrichers)
            {
                enricher.Enrich(headers, metadata);
            }

            var documentId = $"FailedMessages/{uniqueId}";
            var timeOfFailure = failureDetails.TimeOfFailure;
            var groups = new List<FailedMessage.FailureGroup>();

            foreach (var enricher in failureEnrichers)
            {
                groups.AddRange(enricher.Enrich(failureDetails));
            }

            string correlationId;
            headers.TryGetValue(Headers.CorrelationId, out correlationId);

            string replyToAddress;
            headers.TryGetValue(Headers.ReplyToAddress, out replyToAddress);

            AddBodyDetails(metadata, bodyStorageClaimsCheck);

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
                            FailureDetails = failureDetails,
                            MessageMetadata = metadata,
                            MessageId = headers[Headers.MessageId],
                            Headers = headers,
                            ReplyToAddress = replyToAddress,
                            Recoverable = recoverable,
                            CorrelationId = correlationId,
                            MessageIntent = intent
                        }, serializer) // Need to specify serializer here because otherwise the $type for EndpointDetails is missing and this causes EventDispatcher to blow up!
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
                        Value = uniqueId
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
                            FailureDetails = failureDetails,
                            MessageMetadata = metadata,
                            MessageId = headers[Headers.MessageId],
                            Headers = headers,
                            ReplyToAddress = replyToAddress,
                            Recoverable = recoverable,
                            CorrelationId = correlationId,
                            MessageIntent = intent
                        }, serializer) // Need to specify serilaizer here because otherwise the $type for EndpointDetails is missing and this causes EventDispatcher to blow up!
                    },
                    new PatchRequest
                    {
                        Name = nameof(FailedMessage.FailureGroups),
                        Type = PatchCommandType.Set,
                        Value = RavenJToken.FromObject(groups)
                    }
                },
                Metadata = jObjectMetadata
            };
        }
    }
}