namespace ServiceControl.Persistence.EFCore.Implementation;

using ServiceControl.Persistence.EFCore.Infrastructure;

public class FakeBodyStoragePersistence : IBodyStoragePersistence
{
    public Task DeleteBody(string bodyId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<MessageBodyFileResult?> ReadBody(string bodyId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task WriteBody(string bodyId, ReadOnlyMemory<byte> body, string contentType, CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
