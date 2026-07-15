namespace ServiceControl.Persistence.EFCore.Implementation;

using ServiceControl.Operations.BodyStorage;

public class BodyStorage : IBodyStorage
{
    public Task<MessageBodyStreamResult> TryFetch(string bodyId) =>
        throw new NotImplementedException();
}
