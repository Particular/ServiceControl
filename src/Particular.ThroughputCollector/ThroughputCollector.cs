﻿namespace Particular.ThroughputCollector;

using System.Threading;
using AuditThroughput;
using Contracts;
using MonitoringThroughput;
using Persistence;
using ServiceControl.Transports;
using Shared;
using QueueThroughput = Contracts.QueueThroughput;

public class ThroughputCollector(IThroughputDataStore dataStore, ThroughputSettings throughputSettings, IAuditQuery auditQuery, MonitoringService monitoringService, IBrokerThroughputQuery? throughputQuery = null)
    : IThroughputCollector
{
    public async Task<ThroughputConnectionSettings> GetThroughputConnectionSettingsInformation(CancellationToken cancellationToken)
    {
        var throughputConnectionSettings = new ThroughputConnectionSettings
        {
            ServiceControlSettings = ServiceControlSettings.GetServiceControlConnectionSettings(),
            MonitoringSettings = ServiceControlSettings.GetMonitoringConnectionSettings(),
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
        var endpoints = (await dataStore.GetAllEndpoints(false, cancellationToken)).ToList();
        var queueNames = endpoints.Select(endpoint => endpoint.SanitizedName).Distinct();
        var endpointThroughputPerQueue = await dataStore.GetEndpointThroughputByQueueName(queueNames, cancellationToken);
        var endpointSummaries = new List<EndpointThroughputSummary>();

        //group endpoints by sanitized name - so to group throughput recorded from broker, audit and monitoring
        foreach (var endpointGroupPerQueue in endpoints.GroupBy(g => g.SanitizedName))
        {
            var data = new List<ThroughputData>();
            if (endpointThroughputPerQueue.TryGetValue(endpointGroupPerQueue.Key, out var tempData))
            {
                data.AddRange(tempData);
            }

            var isKnownEndpoint = IsKnownEndpoint(endpointGroupPerQueue);
            var endpointSummary = new EndpointThroughputSummary
            {
                //want to display the endpoint name to the user if it's different to the sanitized endpoint name
                Name = endpointGroupPerQueue.FirstOrDefault(endpoint => endpoint.Id.Name != endpoint.SanitizedName)?.Id.Name ?? endpointGroupPerQueue.Key,
                UserIndicator = UserIndicator(endpointGroupPerQueue) ?? (isKnownEndpoint ? Contracts.UserIndicator.NServiceBusEndpoint.ToString() : string.Empty),
                IsKnownEndpoint = isKnownEndpoint,
                MaxDailyThroughput = data.Max(),
            };

            endpointSummaries.Add(endpointSummary);
        }

        return endpointSummaries;
    }

    public async Task<ReportGenerationState> GetReportGenerationState(CancellationToken cancellationToken)
    {
        var reportGenerationState = new ReportGenerationState(transport)
        {
            ReportCanBeGenerated = await dataStore.IsThereThroughputForLastXDays(30, cancellationToken),
        };

        if (!reportGenerationState.ReportCanBeGenerated)
        {
            reportGenerationState.Reason = "24hrs worth of data needs to exist in the last 30 days.";
        }

        return reportGenerationState;
    }

    public async Task<SignedReport> GenerateThroughputReport(string spVersion, DateTime? reportEndDate, CancellationToken cancellationToken)
    {
        (string Mask, string Replacement)[] masks = [];
        var reportMasks = await dataStore.GetReportMasks(cancellationToken);
        CreateMasks(reportMasks.ToArray());

        var endpoints = (await dataStore.GetAllEndpoints(false, cancellationToken)).ToArray();
        var queueNames = endpoints.Select(endpoint => endpoint.SanitizedName).Distinct();
        var endpointThroughputPerQueue = await dataStore.GetEndpointThroughputByQueueName(queueNames, cancellationToken);
        var queueThroughputs = new List<QueueThroughput>();
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

            var userIndicator = UserIndicator(endpointGroupPerQueue) ?? null;
            var notAnNsbEndpoint = userIndicator?.Equals(Contracts.UserIndicator.NotNServiceBusEndpoint.ToString(), StringComparison.OrdinalIgnoreCase) ?? false;

            //get all data that we have, including daily values
            var queueThroughput = new QueueThroughput
            {
                QueueName = Mask(endpointName),
                UserIndicator = userIndicator,
                EndpointIndicators = EndpointIndicators(endpointGroupPerQueue) ?? [],
                NoDataOrSendOnly = throughputData.Sum() == 0,
                Scope = EndpointScope(endpointGroupPerQueue) ?? "",
                Throughput = throughputData.Max(),
                DailyThroughputFromAudit = throughputData.FromSource(ThroughputSource.Audit).Select(s => new DailyThroughput { DateUTC = s.DateUTC, MessageCount = s.MessageCount }).ToArray(),
                DailyThroughputFromMonitoring = throughputData.FromSource(ThroughputSource.Monitoring).Select(s => new DailyThroughput { DateUTC = s.DateUTC, MessageCount = s.MessageCount }).ToArray(),
                DailyThroughputFromBroker = notAnNsbEndpoint ? [] : throughputData.FromSource(ThroughputSource.Broker).Select(s => new DailyThroughput { DateUTC = s.DateUTC, MessageCount = s.MessageCount }).ToArray()
            };

            queueThroughputs.Add(queueThroughput);
        }

        var auditServiceMetadata = await dataStore.GetAuditServiceMetadata(cancellationToken);
        var brokerMetaData = await dataStore.GetBrokerMetadata(cancellationToken);
        if (reportEndDate is null || reportEndDate == DateTime.MinValue)
        {
            reportEndDate = DateTime.UtcNow.Date.AddDays(-1);
        }
        var report = new Report
        {
            EndTime = new DateTimeOffset((DateTime)reportEndDate, TimeSpan.Zero),
            CustomerName = throughputSettings.CustomerName, //who the license is registeredTo
            ReportMethod = "Broker",
            ScopeType = brokerMetaData.ScopeType ?? "",
            Prefix = null,
            MessageTransport = transport,
            ToolType = "Throughput Collector",
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

        report.EnvironmentInformation.EnvironmentData.AddOrUpdate(EnvironmentDataType.ServiceControlVersion.ToString(), throughputSettings.ServiceControlVersion);
        report.EnvironmentInformation.EnvironmentData.AddOrUpdate(EnvironmentDataType.ServicePulseVersion.ToString(), spVersion);
        report.EnvironmentInformation.EnvironmentData.AddOrUpdate(EnvironmentDataType.AuditEnabled.ToString(), endpointThroughputPerQueue.HasDataFromSource(ThroughputSource.Audit).ToString());
        report.EnvironmentInformation.EnvironmentData.AddOrUpdate(EnvironmentDataType.MonitoringEnabled.ToString(), endpointThroughputPerQueue.HasDataFromSource(ThroughputSource.Monitoring).ToString());

        var throughputReport = new SignedReport() { ReportData = report, Signature = Signature.SignReport(report) };
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

    static string? UserIndicator(IGrouping<string, Endpoint> endpoint) => endpoint.FirstOrDefault(s => !string.IsNullOrEmpty(s.UserIndicator))?.UserIndicator;

    static string? EndpointScope(IGrouping<string, Endpoint> endpoint) => endpoint.FirstOrDefault(s => !string.IsNullOrEmpty(s.Scope))?.Scope;

    bool IsKnownEndpoint(IGrouping<string, Endpoint> endpoint) => endpoint.Any(s => s.EndpointIndicators != null && s.EndpointIndicators.Contains(EndpointIndicator.KnownEndpoint.ToString()));

    string[]? EndpointIndicators(IGrouping<string, Endpoint> endpoint) => endpoint.Where(w => w.EndpointIndicators?.Any() == true)?.SelectMany(s => s.EndpointIndicators)?.Distinct()?.ToArray();

    string transport = throughputQuery?.MessageTransport ?? throughputSettings.TransportType;
}