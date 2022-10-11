namespace ServiceControl.Audit.Persistence.InMemory
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Auditing;
    using Auditing.MessagesView;
    using ServiceControl.Audit.Monitoring;

    static class MessagesViewFactory
    {
        static ILookup<string, PropertyInfo> writeableMessageViewProperties
            = typeof(MessagesView).GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.CanWrite)
                .ToLookup(p => p.Name, StringComparer.InvariantCultureIgnoreCase);

        static ILookup<string, string> propertyMapping
            = new Dictionary<string, string>
            {
                // Metadata key, Property name
                { "ContentLength", "BodySize" }
            }.ToLookup(
                x => x.Key,
                x => x.Value,
                StringComparer.InvariantCultureIgnoreCase);

        public static MessagesView Create(ProcessedMessage message)
        {
            var result = new MessagesView
            {
                Id = message.UniqueMessageId,
                ProcessedAt = message.ProcessedAt,
                Status = MessageStatus.Successful,
                Headers = message.Headers.Select(header => new KeyValuePair<string, string>(header.Key, header.Value))
            };

            foreach (var metadata in message.MessageMetadata)
            {
                foreach (var property in writeableMessageViewProperties[metadata.Key])
                {
                    // TODO: Check types
                    property.SetValue(result, metadata.Value);
                }
                foreach (var mappedName in propertyMapping[metadata.Key])
                {
                    foreach (var property in writeableMessageViewProperties[mappedName])
                    {
                        // TODO: Check types
                        property.SetValue(result, metadata.Value);
                    }
                }
            }

            if (message.MessageMetadata.TryGetValue("IsRetried", out var isRetried) && (bool)isRetried)
            {
                result.Status = MessageStatus.ResolvedSuccessfully;
            }

            return result;
        }
    }
}