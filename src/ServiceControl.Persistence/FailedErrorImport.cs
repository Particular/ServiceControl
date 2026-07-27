namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
    using ServiceControl.Persistence.Infrastructure;

    public class FailedErrorImport
    {
        public string Id { get; set; }
        public FailedTransportMessage Message { get; set; }
        public string ExceptionInfo { get; set; }

        public static string MakeDocumentId(Guid id) => $"FailedErrorImports/{id}";

        public static Guid DeriveKey(IReadOnlyDictionary<string, string> headers, string nativeMessageId)
        {
            try
            {
                if (Guid.TryParse(headers.UniqueId(), out var uniqueMessageId))
                {
                    return uniqueMessageId;
                }
            }
            catch (Exception)
            {
                // UniqueId() derives the processing endpoint, which throws when the failed message
                // carries no endpoint header. Malformed messages are a leading cause of import
                // failure, so fall back to a key derived from the id the transport always supplies.
            }

            return DeterministicGuid.MakeId(nativeMessageId);
        }
    }
}