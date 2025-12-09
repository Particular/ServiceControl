namespace ServiceControl.Persistence.Sql.Core.Implementation;

using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Particular.LicensingComponent.Contracts;
using Particular.LicensingComponent.Persistence;
using ServiceControl.Persistence.Sql.Core.DbContexts;
using ServiceControl.Persistence.Sql.Core.Entities;

public class LicensingDataStore(IServiceProvider serviceProvider) : ILicensingDataStore
{
    public Task<IEnumerable<Endpoint>> GetAllEndpoints(bool includePlatformEndpoints, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<Endpoint?> GetEndpoint(EndpointIdentifier id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<IEnumerable<(EndpointIdentifier Id, Endpoint? Endpoint)>> GetEndpoints(IList<EndpointIdentifier> endpointIds, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<IDictionary<string, IEnumerable<ThroughputData>>> GetEndpointThroughputByQueueName(IList<string> queueNames, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<bool> IsThereThroughputForLastXDays(int days, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<bool> IsThereThroughputForLastXDaysForSource(int days, ThroughputSource throughputSource, bool includeToday, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task RecordEndpointThroughput(string endpointName, ThroughputSource throughputSource, IList<EndpointDailyThroughput> throughput, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task SaveEndpoint(Endpoint endpoint, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task UpdateUserIndicatorOnEndpoints(List<UpdateUserIndicator> userIndicatorUpdates, CancellationToken cancellationToken) => throw new NotImplementedException();

    #region AuditServiceMetadata

    static readonly AuditServiceMetadata EmptyAuditServiceMetadata = new([], []);
    public Task SaveAuditServiceMetadata(AuditServiceMetadata auditServiceMetadata, CancellationToken cancellationToken)
        => SaveMetdata("AuditServiceMetadata", auditServiceMetadata, cancellationToken);
    public async Task<AuditServiceMetadata> GetAuditServiceMetadata(CancellationToken cancellationToken = default)
        => await LoadMetadata<AuditServiceMetadata>("AuditServiceMetadata", cancellationToken)
           ?? EmptyAuditServiceMetadata;

    #endregion

    #region ReportMasks
    static readonly List<string> EmptyReportMasks = [];
    public Task SaveReportMasks(List<string> reportMasks, CancellationToken cancellationToken)
        => SaveMetdata("ReportMasks", reportMasks, cancellationToken);
    public async Task<List<string>> GetReportMasks(CancellationToken cancellationToken)
        => await LoadMetadata<List<string>>("ReportMasks", cancellationToken) ?? EmptyReportMasks;

    #endregion

    #region Broker Metadata
    static readonly BrokerMetadata EmptyBrokerMetada = new(ScopeType: null, []);

    public Task SaveBrokerMetadata(BrokerMetadata brokerMetadata, CancellationToken cancellationToken)
        => SaveMetdata("BrokerMetadata", brokerMetadata, cancellationToken);

    public async Task<BrokerMetadata> GetBrokerMetadata(CancellationToken cancellationToken)
        => await LoadMetadata<BrokerMetadata>("BrokerMetadata", cancellationToken) ?? EmptyBrokerMetada;
    #endregion

    #region Metadata
    async Task<T?> LoadMetadata<T>(string key, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        await using var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContextBase>();
        var existing = await dbContext.LicensingMetadata.SingleOrDefaultAsync(m => m.Key == key, cancellationToken);
        if (existing is null)
        {
            return default;
        }
        return JsonSerializer.Deserialize<T>(existing.Data);

    }

    async Task SaveMetdata<T>(string key, T data, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        await using var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContextBase>();

        var existing = await dbContext.LicensingMetadata.SingleOrDefaultAsync(m => m.Key == key, cancellationToken);

        var serialized = JsonSerializer.Serialize(data);

        if (existing is null)
        {
            LicensingMetadataEntity newMetadata = new()
            {
                Key = key,
                Data = serialized
            };
            _ = await dbContext.LicensingMetadata.AddAsync(newMetadata, cancellationToken);
        }
        else
        {
            existing.Data = serialized;
            _ = dbContext.LicensingMetadata.Update(existing);
        }

        _ = await dbContext.SaveChangesAsync(cancellationToken);
    }
    #endregion
}
