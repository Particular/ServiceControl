namespace ServiceControl.Persistence.EFCore.DataMigration;

using ServiceControl.Operations;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;

static class FailedErrorImportRowMapper
{
    public static FailedErrorImportEntity Map(FailedErrorImport import, DateTime failedAt, bool bodyStoredExternally)
    {
        var message = import.Message!;

        return new FailedErrorImportEntity
        {
            // The key a live import of the same message derives, so a later failure of that message on SQL updates this row rather than adding a second.
            UniqueMessageId = FailedErrorImport.DeriveKey(message.Headers, message.Id),
            FailedAt = failedAt,
            MessageId = message.Id,
            HeadersJson = MessageHeaders.Write(message.Headers),
            Body = bodyStoredExternally ? [] : message.Body ?? [],
            BodyStoredExternally = bodyStoredExternally,
            ExceptionInfo = import.ExceptionInfo ?? string.Empty
        };
    }
}
