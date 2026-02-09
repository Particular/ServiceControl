namespace ServiceControl.Audit.Persistence.Sql.Core.Infrastructure;

public interface IBodyStoragePersistence
{
    Task WriteBodyAsync(string bodyId, ReadOnlyMemory<byte> body, string contentType, Guid batchId, CancellationToken cancellationToken = default);
    Task<MessageBodyFileResult?> ReadBodyAsync(string bodyId, Guid batchId, CancellationToken cancellationToken = default);
    Task DeleteBatches(IEnumerable<Guid> batchIds, CancellationToken cancellationToken = default);
}
