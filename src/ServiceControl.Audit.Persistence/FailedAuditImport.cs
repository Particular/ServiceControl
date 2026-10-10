namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Collections.Generic;
    using ServiceControl.Audit.Persistence.Infrastructure;

    public class FailedAuditImport
    {
        public string Id { get; set; }
        public FailedTransportMessage Message { get; set; }
        public string ExceptionInfo { get; set; }

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
                // Malformed messages may have no processing endpoint; the transport ID is always available.
            }

            return DeterministicGuid.MakeId(nativeMessageId);
        }
    }
}