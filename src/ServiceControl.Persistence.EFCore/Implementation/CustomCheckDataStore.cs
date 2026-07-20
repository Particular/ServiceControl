namespace ServiceControl.Persistence.EFCore.Implementation;

using ServiceControl.Contracts.CustomChecks;
using ServiceControl.Persistence.Infrastructure;

public class CustomCheckDataStore : ICustomChecksDataStore
{
    public Task<CheckStateChange> UpdateCustomCheckStatus(CustomCheckDetail detail) =>
        throw new NotImplementedException();

    public Task<QueryResult<IList<CustomCheck>>> GetStats(PagingInfo paging, string? status = null) =>
        throw new NotImplementedException();

    public Task DeleteCustomCheck(Guid id) =>
        throw new NotImplementedException();

    public Task<int> GetNumberOfFailedChecks() =>
        throw new NotImplementedException();
}
