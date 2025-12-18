namespace ServiceControl.Persistence.Sql;

using System.Threading.Tasks;
using ServiceControl.Operations.BodyStorage;

class NoOpBodyStorage : IBodyStorage
{
    public Task<MessageBodyStreamResult> TryFetch(string bodyId) =>
        Task.FromResult(new MessageBodyStreamResult { HasResult = false });
}
