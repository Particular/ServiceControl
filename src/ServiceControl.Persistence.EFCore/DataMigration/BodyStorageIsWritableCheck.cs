namespace ServiceControl.Persistence.EFCore.DataMigration;

using System.Text;
using Infrastructure;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Writes, reads and deletes one probe body, so a misconfigured body store stops the copy before it starts.
/// Bodies can live on a file share, in Azure Blob Storage or in S3, and without this check the first category
/// that carries bodies would only find out part way through, when a body it cannot write fails the batch and stops the category.
/// </summary>
sealed class BodyStorageIsWritableCheck(IBodyStoragePersistence bodyStorage) : IMigrationStartupCheck
{
    const string ProbeBodyId = "migration-writable-probe";

    public string Name => "message body storage is writable";

    public async Task Run(CancellationToken cancellationToken = default)
    {
        await bodyStorage.WriteBody(ProbeBodyId, Encoding.UTF8.GetBytes(ProbeBodyId), "text/plain", cancellationToken);

        try
        {
            var probe = await bodyStorage.ReadBody(ProbeBodyId, cancellationToken)
                ?? throw new Exception($"Message body storage accepted the probe body '{ProbeBodyId}' and then did not return it.");

            await probe.Stream.DisposeAsync();
        }
        finally
        {
            // The probe is not migrated data, so it goes even when the read fails: a probe left behind is a body the source never had.
            await bodyStorage.DeleteBodyIfExists(ProbeBodyId, cancellationToken);
        }
    }
}
