namespace Particular.LicensingComponent;

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using AuditThroughput;
using Contracts;
using MonitoringThroughput;
using Persistence;
using Report;
using ServiceControl.Transports.BrokerThroughput;
using Shared;
using QueueThroughput = Report.QueueThroughput;

public class ThroughputCollector(ILicensingDataStore dataStore, ThroughputSettings throughputSettings, IAuditQuery auditQuery, MonitoringService monitoringService, IBrokerThroughputQuery? throughputQuery = null)
    : IThroughputCollector
{
    public async Task<ThroughputConnectionSettings> GetThroughputConnectionSettingsInformation(CancellationToken cancellationToken)
    {
        var throughputConnectionSettings = new ThroughputConnectionSettings
        {
            ServiceControlSettings = ServiceControlSettings.GetServiceControlConnectionSettings(),
            MonitoringSettings = throughputSettings.TransportType == "MSMQ" ? ServiceControlSettings.GetMonitoringConnectionSettings() : [],
            BrokerSettings = throughputQuery?.Settings.Select(pair => new ThroughputConnectionSetting($"{ThroughputSettings.SettingsNamespace.Root}/{pair.Key}", pair.Description)).ToList() ?? []
        };
        return await Task.FromResult(throughputConnectionSettings);
    }

    public async Task<ConnectionTestResults> TestConnectionSettings(CancellationToken cancellationToken)
    {
        var tasks = new List<Task>();
        var brokerTask = Task.FromResult(new ConnectionSettingsTestResult { ConnectionSuccessful = false, ConnectionErrorMessages = [] });

        if (throughputQuery != null)
        {
            brokerTask = throughputQuery.TestConnection(cancellationToken).ContinueWith(task =>
            {
                var (success, errors, diagnostics) = task.Result;
                return new ConnectionSettingsTestResult { ConnectionSuccessful = success, ConnectionErrorMessages = errors, Diagnostics = diagnostics };
            }, cancellationToken);
            tasks.Add(brokerTask);
        }

        var auditTask = auditQuery.TestAuditConnection(cancellationToken);
        tasks.Add(auditTask);

        var monitoringTask = monitoringService.TestMonitoringConnection(cancellationToken);
        tasks.Add(monitoringTask);

        await Task.WhenAll(tasks);

        var connectionTestResults = new ConnectionTestResults(transport, auditTask.Result, monitoringTask.Result, brokerTask.Result);

        return await Task.FromResult(connectionTestResults);
    }

    public async Task UpdateUserIndicatorsOnEndpoints(List<UpdateUserIndicator> userIndicatorUpdates, CancellationToken cancellationToken) =>
        await dataStore.UpdateUserIndicatorOnEndpoints(userIndicatorUpdates, cancellationToken);

    public async Task<List<string>> GetReportMasks(CancellationToken cancellationToken) => await dataStore.GetReportMasks(cancellationToken);
    public async Task UpdateReportMasks(List<string> reportMaskUpdates, CancellationToken cancellationToken) => await dataStore.SaveReportMasks(reportMaskUpdates, cancellationToken);

    public async Task<List<EndpointThroughputSummary>> GetThroughputSummary(CancellationToken cancellationToken)
    {
        var endpointSummaries = new List<EndpointThroughputSummary>();

        await foreach (var endpointData in GetDistinctEndpointData(cancellationToken))
        {
            var endpointSummary = new EndpointThroughputSummary
            {
                Name = endpointData.Name,
                UserIndicator = endpointData.UserIndicator ?? (endpointData.IsKnownEndpoint ? Contracts.UserIndicator.NServiceBusEndpoint.ToString() : string.Empty),
                IsKnownEndpoint = endpointData.IsKnownEndpoint,
                MaxDailyThroughput = endpointData.ThroughputData.MaxDailyThroughput(),
                MaxMonthlyThroughput = endpointData.ThroughputData.MaxMonthlyThroughput()
            };

            endpointSummaries.Add(endpointSummary);
        }

        return endpointSummaries;
    }

    public async Task<ReportGenerationState> GetReportGenerationState(CancellationToken cancellationToken) =>
        throughputQuery == null ?
            await GetReportGenerationStateForNonBroker(cancellationToken) :
            await GetReportGenerationStateForBroker(cancellationToken);

    async Task<ReportGenerationState> GetReportGenerationStateForBroker(CancellationToken cancellationToken)
    {
        var reportCanBeGenerated = await dataStore.IsThereThroughputForLastXDaysForSource(30, ThroughputSource.Broker, false, cancellationToken);
        var reason = reportCanBeGenerated ? string.Empty : "Need at least one day of usage data in the last 30 days from the configured broker.";

        return new ReportGenerationState(transport)
        {
            ReportCanBeGenerated = reportCanBeGenerated,
            Reason = reason
        };
    }

    async Task<ReportGenerationState> GetReportGenerationStateForNonBroker(CancellationToken cancellationToken)
    {
        var reportCanBeGenerated = await dataStore.IsThereThroughputForLastXDays(30, cancellationToken);
        var reason = reportCanBeGenerated ? string.Empty : $"Need at least one day of usage data in the last 30 days.";

        return new ReportGenerationState(transport)
        {
            ReportCanBeGenerated = reportCanBeGenerated,
            Reason = reason
        };
    }

    public async Task<SignedReport> GenerateThroughputReport(string spVersion, DateTime? reportEndDate, CancellationToken cancellationToken)
    {
        (string Mask, string Replacement)[] masks = [];
        var reportMasks = await dataStore.GetReportMasks(cancellationToken);
        CreateMasks(reportMasks.ToArray());

        var queueThroughputs = new List<QueueThroughput>();
        List<string> ignoredQueueNames = [];

        await foreach (var endpointData in GetDistinctEndpointData(cancellationToken))
        {
            var notAnNsbEndpoint = endpointData.UserIndicator?.Equals(Contracts.UserIndicator.NotNServiceBusEndpoint.ToString(), StringComparison.OrdinalIgnoreCase) ?? false;

            //get all data that we have, including daily values
            var queueThroughput = new QueueThroughput
            {
                QueueName = Mask(endpointData.Name),
                UserIndicator = endpointData.UserIndicator,
                EndpointIndicators = endpointData.EndpointIndicators ?? [],
                NoDataOrSendOnly = endpointData.ThroughputData.Sum() == 0,
                Scope = endpointData.Scope ?? "",
                Throughput = endpointData.ThroughputData.MaxDailyThroughput(),
                DailyThroughputFromAudit = endpointData.ThroughputData.FromSource(ThroughputSource.Audit).Select(s => new DailyThroughput { DateUTC = s.DateUTC, MessageCount = s.MessageCount }).ToArray(),
                DailyThroughputFromMonitoring = endpointData.ThroughputData.FromSource(ThroughputSource.Monitoring).Select(s => new DailyThroughput { DateUTC = s.DateUTC, MessageCount = s.MessageCount }).ToArray(),
                DailyThroughputFromBroker = notAnNsbEndpoint ? [] : endpointData.ThroughputData.FromSource(ThroughputSource.Broker).Select(s => new DailyThroughput { DateUTC = s.DateUTC, MessageCount = s.MessageCount }).ToArray()
            };

            queueThroughputs.Add(queueThroughput);
        }

        var auditServiceMetadata = await dataStore.GetAuditServiceMetadata(cancellationToken);
        var brokerMetaData = await dataStore.GetBrokerMetadata(cancellationToken);
        if (reportEndDate is null || reportEndDate == DateTime.MinValue)
        {
            reportEndDate = DateTime.UtcNow.Date.AddDays(-1);
        }
        var report = new Report.Report
        {
            EndTime = new DateTimeOffset((DateTime)reportEndDate, TimeSpan.Zero),
            CustomerName = throughputSettings.CustomerName, //who the license is registeredTo
            ReportMethod = throughputQuery == null ? "ServiceControl" : "Broker",
            ScopeType = brokerMetaData.ScopeType ?? "",
            Prefix = null,
            MessageTransport = transport,
            ToolType = "Platform Licensing Component",
            ToolVersion = throughputSettings.ServiceControlVersion,
            IgnoredQueues = [.. ignoredQueueNames],
            Queues = [.. queueThroughputs],
            TotalQueues = queueThroughputs.Count,
            TotalThroughput = queueThroughputs.Sum(q => q.Throughput ?? 0),
            EnvironmentInformation = new EnvironmentInformation { AuditServicesData = new AuditServicesData(auditServiceMetadata.Versions, auditServiceMetadata.Transports), EnvironmentData = brokerMetaData.Data }
        };

        var auditThroughput = queueThroughputs.SelectMany(w => w.DailyThroughputFromAudit);
        var monitoringThroughput = queueThroughputs.SelectMany(w => w.DailyThroughputFromMonitoring);
        var brokerThroughput = queueThroughputs.SelectMany(w => w.DailyThroughputFromBroker);

        //this will be the date of the first throughput that we have received
        var firstAuditThroughputDate = auditThroughput is not null && auditThroughput.Any() ? auditThroughput.MinBy(m => m.DateUTC)!.DateUTC.ToDateTime(TimeOnly.MinValue) : ((DateTime)reportEndDate).AddDays(-1);
        var firstMonitoringThroughputDate = monitoringThroughput is not null && monitoringThroughput.Any() ? monitoringThroughput.MinBy(m => m.DateUTC)!.DateUTC.ToDateTime(TimeOnly.MinValue) : ((DateTime)reportEndDate).AddDays(-1);
        var firstBrokerThroughputDate = brokerThroughput is not null && brokerThroughput.Any() ? brokerThroughput.MinBy(m => m.DateUTC)!.DateUTC.ToDateTime(TimeOnly.MinValue) : ((DateTime)reportEndDate).AddDays(-1);
        report.StartTime = new DateTimeOffset(new[] { firstAuditThroughputDate, firstMonitoringThroughputDate, firstBrokerThroughputDate }.Min(), TimeSpan.Zero);
        report.ReportDuration = report.EndTime - report.StartTime;

        report.EnvironmentInformation.EnvironmentData[EnvironmentDataType.ServiceControlVersion.ToString()] = throughputSettings.ServiceControlVersion;
        report.EnvironmentInformation.EnvironmentData[EnvironmentDataType.ServicePulseVersion.ToString()] = spVersion;
        report.EnvironmentInformation.EnvironmentData[EnvironmentDataType.AuditEnabled.ToString()] = systemHasAuditEnabled.ToString();
        report.EnvironmentInformation.EnvironmentData[EnvironmentDataType.MonitoringEnabled.ToString()] = systemHasMonitoringEnabled.ToString();

        var throughputReport = new SignedReport { ReportData = report, Signature = Signature.SignReport(report) };
        return throughputReport;

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
    }

    async IAsyncEnumerable<EndpointData> GetDistinctEndpointData([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var endpoints = (await dataStore.GetAllEndpoints(false, cancellationToken)).ToArray();
        var queueNames = endpoints.Select(endpoint => endpoint.SanitizedName).Distinct().ToList();
        var endpointThroughputPerQueue = await dataStore.GetEndpointThroughputByQueueName(queueNames, cancellationToken);

        systemHasAuditEnabled = endpointThroughputPerQueue.HasDataFromSource(ThroughputSource.Audit);
        systemHasMonitoringEnabled = endpointThroughputPerQueue.HasDataFromSource(ThroughputSource.Monitoring);

        //group endpoints by sanitized name - so to group throughput recorded from broker, audit and monitoring
        //some brokers use lowercase only so we want to ensure that we are matching on what the user has setup via NSB in terms of nn endpoint name, and what is stored on the broker
        foreach (var endpointGroupPerQueue in endpoints.GroupBy(g => CleanseSanitizedName(g.SanitizedName)))
        {
            //want to display the endpoint name if it's different to the sanitized endpoint name
            var endpointName = endpointGroupPerQueue.FirstOrDefault(endpoint => !string.Equals(endpoint.Id.Name, endpointGroupPerQueue.Key, StringComparison.Ordinal))?.Id.Name ?? endpointGroupPerQueue.Key;

            var throughputData = new List<ThroughputData>();
            foreach (var endpointQueueName in endpointThroughputPerQueue.Keys.Where(k => CleanseSanitizedName(k) == endpointGroupPerQueue.Key))
            {
                if (endpointThroughputPerQueue.TryGetValue(endpointQueueName, out var tempData))
                {
                    throughputData.AddRange(tempData);
                }
            }

            var userIndicator = UserIndicator(endpointGroupPerQueue) ?? null;

            yield return new EndpointData(endpointName, throughputData, userIndicator, EndpointScope(endpointGroupPerQueue), EndpointIndicators(endpointGroupPerQueue), IsKnownEndpoint(endpointGroupPerQueue));
        }
    }

    class EndpointData
    {
        internal EndpointData(string name, List<ThroughputData> throughputData, string? userIndicator, string? scope, string[]? endpointIndicators, bool isKnownEndpoint)
        {
            Name = name;
            ThroughputData = throughputData;
            UserIndicator = userIndicator;
            Scope = scope;
            EndpointIndicators = endpointIndicators;
            IsKnownEndpoint = isKnownEndpoint;
        }

        internal string Name { get; }
        internal List<ThroughputData> ThroughputData { get; }
        internal string? UserIndicator { get; }
        internal string? Scope { get; }
        internal string[]? EndpointIndicators { get; }
        internal bool IsKnownEndpoint { get; }
    }

    string CleanseSanitizedName(string endpointName)
    {
        return throughputQuery == null ? endpointName : throughputQuery.SanitizedEndpointNameCleanser(endpointName);
    }
    static string? UserIndicator(IGrouping<string, Endpoint> endpoint) => endpoint.FirstOrDefault(s => !string.IsNullOrEmpty(s.UserIndicator))?.UserIndicator;

    static string? EndpointScope(IGrouping<string, Endpoint> endpoint) => endpoint.FirstOrDefault(s => !string.IsNullOrEmpty(s.Scope))?.Scope;

    bool IsKnownEndpoint(IGrouping<string, Endpoint> endpoint) => endpoint.Any(s => s.EndpointIndicators != null && s.EndpointIndicators.Contains(EndpointIndicator.KnownEndpoint.ToString()));

    string[]? EndpointIndicators(IGrouping<string, Endpoint> endpoint) => endpoint.Where(w => w.EndpointIndicators?.Any() == true)?.SelectMany(s => s.EndpointIndicators)?.Distinct()?.ToArray();

    readonly string transport = throughputQuery?.MessageTransport ?? throughputSettings.TransportType;
    internal bool systemHasAuditEnabled;
    internal bool systemHasMonitoringEnabled;
}