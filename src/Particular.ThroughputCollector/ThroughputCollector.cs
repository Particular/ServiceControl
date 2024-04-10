namespace Particular.ThroughputCollector;

using AuditThroughput;
using Contracts;
using Particular.ThroughputCollector.MonitoringThroughput;
using Persistence;
using ServiceControl.Transports;
using Shared;
using System.Collections.Generic;
using System.Threading.Tasks;

public class ThroughputCollector(IThroughputDataStore dataStore, ThroughputSettings throughputSettings, IAuditQuery auditQuery, MonitoringService monitoringService, IBrokerThroughputQuery? throughputQuery = null)
    : IThroughputCollector
{
    public async Task<ThroughputConnectionSettings> GetThroughputConnectionSettingsInformation()
    {
        var throughputConnectionSettings = new ThroughputConnectionSettings
        {
            Broker = throughputSettings.Broker,
            Settings = throughputSettings.Broker != Broker.None
                ? throughputQuery?.Settings.Select(pair => new ThroughputConnectionSetting(pair.Key, pair.Description)).ToList() ?? []
                : ServiceControlSettings.GetServiceControlConnectionSettings()
        };
        return await Task.FromResult(throughputConnectionSettings);
    }

    public async Task<ConnectionTestResults> TestConnectionSettings(CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task>();
        var brokerTask = Task.FromResult(new ConnectionSettingsTestResult { ConnectionSuccessful = false, ConnectionErrorMessages = [] });

        if (throughputQuery != null)
        {
            brokerTask = throughputQuery.TestConnection(cancellationToken).ContinueWith(task => new ConnectionSettingsTestResult { ConnectionSuccessful = task.Result.Success, ConnectionErrorMessages = task.Result.Errors }, cancellationToken);
            tasks.Add(brokerTask);
        }

        var auditTask = auditQuery.TestAuditConnection(cancellationToken);
        tasks.Add(auditTask);

        var monitoringTask = monitoringService.TestMonitoringConnection(cancellationToken);
        tasks.Add(monitoringTask);

        await Task.WhenAll(tasks);

        var connectionTestResults = new ConnectionTestResults(throughputSettings.Broker, auditTask.Result, monitoringTask.Result, brokerTask.Result);

        return await Task.FromResult(connectionTestResults);
    }

    public async Task UpdateUserIndicatorsOnEndpoints(List<EndpointThroughputSummary> endpointThroughputs)
    {
        await dataStore.UpdateUserIndicatorOnEndpoints(endpointThroughputs.Select(e =>
            new Endpoint(e.Name, ThroughputSource.None)
            {
                SanitizedName = e.Name,
                UserIndicator = e.UserIndicator,
            }).ToList());

        await Task.CompletedTask;
    }

    public async Task<List<EndpointThroughputSummary>> GetThroughputSummary(CancellationToken cancellationToken = default)
    {
        var endpoints = (await dataStore.GetAllEndpoints(false, cancellationToken)).ToList();
        var queueNames = endpoints.Select(endpoint => endpoint.SanitizedName).Distinct();
        var endpointThroughputPerQueue = await dataStore.GetEndpointThroughputByQueueName(queueNames, cancellationToken);
        var endpointSummaries = new List<EndpointThroughputSummary>();

        //group endpoints by sanitized name - so to group throughput recorded from broker, audit and monitoring
        foreach (var endpointGroupPerQueue in endpoints.GroupBy(g => g.SanitizedName))
        {
            if (!endpointThroughputPerQueue.TryGetValue(endpointGroupPerQueue.Key, out var data))
            {
                data = [];
            }

            var endpointSummary = new EndpointThroughputSummary
            {
                //want to display the endpoint name to the user if it's different to the sanitized endpoint name
                Name = endpointGroupPerQueue.FirstOrDefault(endpoint => endpoint.Id.Name != endpoint.SanitizedName)?.Id.Name ?? endpointGroupPerQueue.Key,
                UserIndicator = UserIndicator(endpointGroupPerQueue) ?? string.Empty,
                IsKnownEndpoint = IsKnownEndpoint(endpointGroupPerQueue),
                MaxDailyThroughput = data.Max(),
            };

            endpointSummaries.Add(endpointSummary);
        }

        return endpointSummaries;
    }

    public async Task<ReportGenerationState> GetReportGenerationState()
    {
        var reportGenerationState = new ReportGenerationState
        {
            Broker = throughputSettings.Broker,
            ReportCanBeGenerated = await dataStore.IsThereThroughputForLastXDays(30)
        };

        return reportGenerationState;
    }

    public async Task<SignedReport> GenerateThroughputReport(string[] masks, string spVersion, CancellationToken cancellationToken)
    {
        CreateMasks(masks);

        var endpoints = await dataStore.GetAllEndpoints(false, cancellationToken);
        var queueNames = endpoints.Select(endpoint => endpoint.SanitizedName).Distinct();
        var endpointThroughputPerQueue = await dataStore.GetEndpointThroughputByQueueName(queueNames, cancellationToken);
        var queueThroughputs = new List<Contracts.QueueThroughput>();
        List<string> ignoredQueueNames = [];

        //group endpoints by sanitized name - so to group throughput recorded from broker, audit and monitoring
        foreach (var endpointGroupPerQueue in endpoints.GroupBy(g => g.SanitizedName))
        {
            //want to display the endpoint name if it's different to the sanitized endpoint name
            var endpointName = endpointGroupPerQueue.FirstOrDefault(endpoint => endpoint.Id.Name != endpoint.SanitizedName)?.Id.Name ?? endpointGroupPerQueue.Key;

            if (!endpointThroughputPerQueue.TryGetValue(endpointGroupPerQueue.Key, out var data))
            {
                data = [];
            }

            var throughputData = data.ToList();

            var userIndicator = UserIndicator(endpointGroupPerQueue) ?? string.Empty;
            var notAnNsbEndpoint = userIndicator.Equals(Contracts.UserIndicator.NotNServicebusEndpoint.ToString(), StringComparison.OrdinalIgnoreCase);

            //get all data that we have, including daily values
            var queueThroughput = new Contracts.QueueThroughput
            {
                QueueName = Mask(endpointName),
                UserIndicator = userIndicator,
                EndpointIndicators = EndpointIndicators(endpointGroupPerQueue) ?? [],
                NoDataOrSendOnly = throughputData.Sum() == 0,
                Scope = EndpointScope(endpointGroupPerQueue) ?? "",
                Throughput = throughputData.Max(),
                DailyThroughputFromAudit = throughputData.FromSource(ThroughputSource.Audit).ToArray(),
                DailyThroughputFromMonitoring = throughputData.FromSource(ThroughputSource.Monitoring).ToArray(),
                DailyThroughputFromBroker = notAnNsbEndpoint ? [] : throughputData.FromSource(ThroughputSource.Broker).ToArray()
            };

            queueThroughputs.Add(queueThroughput);
        }

        var brokerData = await dataStore.GetBrokerData(throughputSettings.Broker);
        var yesterday = DateTime.UtcNow.Date.AddDays(-1);
        var report = new Report
        {
            EndTime = new DateTimeOffset(yesterday, TimeSpan.Zero),
            CustomerName = throughputSettings.CustomerName, //who the license is registeredTo
            ReportMethod = "NA",
            ScopeType = brokerData?.ScopeType ?? "",
            Prefix = null,
            MessageTransport = throughputQuery?.MessageTransport ?? throughputSettings.TransportType,
            ToolVersion = "V3", //ensure we check for this on the other side - ie that we can process V3
            ServiceControlVersion = throughputSettings.ServiceControlVersion,
            ServicePulseVersion = spVersion,
            IgnoredQueues = [.. ignoredQueueNames],
            Queues = [.. queueThroughputs],
            TotalQueues = queueThroughputs.Count,
            TotalThroughput = queueThroughputs.Sum(q => q.Throughput ?? 0),
            EnvironmentData = brokerData?.Data ?? []
        };

        var auditThroughput = queueThroughputs.SelectMany(w => w.DailyThroughputFromAudit);
        var monitoringThroughput = queueThroughputs.SelectMany(w => w.DailyThroughputFromMonitoring);
        var brokerThroughput = queueThroughputs.SelectMany(w => w.DailyThroughputFromBroker);

        //this will be the date of the first throughput that we have received
        var firstAuditThroughputDate = auditThroughput.Any() ? auditThroughput.MinBy(m => m.DateUTC).DateUTC.ToDateTime(TimeOnly.MinValue) : yesterday.AddDays(-1);
        var firstMonitoringThroughputDate = monitoringThroughput.Any() ? monitoringThroughput.MinBy(m => m.DateUTC).DateUTC.ToDateTime(TimeOnly.MinValue) : yesterday.AddDays(-1);
        var firstBrokerThroughputDate = brokerThroughput.Any() ? brokerThroughput.MinBy(m => m.DateUTC).DateUTC.ToDateTime(TimeOnly.MinValue) : yesterday.AddDays(-1);
        report.StartTime = new DateTimeOffset(new[] { firstAuditThroughputDate, firstMonitoringThroughputDate, firstBrokerThroughputDate }.Min(), TimeSpan.Zero);
        report.ReportDuration = report.EndTime - report.StartTime;

        report.EnvironmentData.Add(EnvironmentData.AuditEnabled.ToString(), endpointThroughputPerQueue.HasDataFromSource(ThroughputSource.Audit).ToString());
        report.EnvironmentData.Add(EnvironmentData.MonitoringEnabled.ToString(), endpointThroughputPerQueue.HasDataFromSource(ThroughputSource.Monitoring).ToString());

        var throughputReport = new SignedReport() { ReportData = report, Signature = Signature.SignReport(report) };
        return throughputReport;
    }

    void CreateMasks(string[] wordsToMask)
    {
        var number = 0;
        masks = wordsToMask
            .Select(mask =>
            {
                number++;
                return (mask, $"REDACTED{number}");
            })
            .ToArray();
    }

    string Mask(string stringToMask)
    {
        foreach (var (mask, replacement) in masks)
        {
            stringToMask = stringToMask.Replace(mask, replacement, StringComparison.OrdinalIgnoreCase);
        }

        return stringToMask;
    }

    static string? UserIndicator(IGrouping<string, Endpoint> endpoint) => endpoint.FirstOrDefault(s => !string.IsNullOrEmpty(s.UserIndicator))?.UserIndicator;

    static string? EndpointScope(IGrouping<string, Endpoint> endpoint) => endpoint.FirstOrDefault(s => !string.IsNullOrEmpty(s.Scope))?.Scope;

    bool IsKnownEndpoint(IGrouping<string, Endpoint> endpoint) => endpoint.Any(s => s.EndpointIndicators != null && s.EndpointIndicators.Contains(EndpointIndicator.KnownEndpoint.ToString()));

    string[]? EndpointIndicators(IGrouping<string, Endpoint> endpoint) => endpoint.Where(w => w.EndpointIndicators?.Any() == true)?.SelectMany(s => s.EndpointIndicators)?.Distinct()?.ToArray();
    (string Mask, string Replacement)[] masks = [];
}