namespace ServiceControl.Audit.Persistence.Sql.Core.Infrastructure;

public interface IBodyStoragePersistence
{
    Task DeleteBodies(IEnumerable<string> bodyIds, CancellationToken cancellationToken = default);
    Task<MessageBodyFileResult?> ReadBodyAsync(string bodyId, CancellationToken cancellationToken = default);
    Task WriteBodyAsync(string bodyId, ReadOnlyMemory<byte> body, string contentType, CancellationToken cancellationToken = default);
}
