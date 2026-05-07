#nullable enable
namespace ServiceControl.Transports.IBMMQ;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using BrokerThroughput;
using IBM.WMQ;
using IBM.WMQ.PCF;
using Microsoft.Extensions.Logging;

class IBMMQQuery(
    ILogger<IBMMQQuery> logger,
    TimeProvider timeProvider,
    TransportSettings transportSettings) : BrokerThroughputQuery(logger, "IBMMQ")
{
    string queueManagerName = string.Empty;
    Hashtable connectionProperties = [];
    string statisticsQueueName = IBMMQSettings.DefaultStatisticsQueue;
    string? forwardingQueueName;

    readonly SemaphoreSlim cacheLock = new(1, 1);
    readonly Dictionary<string, Dictionary<DateOnly, long>> throughputCache = new(StringComparer.Ordinal);
    bool cachePopulated;

    protected override void InitializeCore(ReadOnlyDictionary<string, string> settings)
    {
        // Initialize is synchronous and runs at host startup, so it must not block on broker I/O.
        // Connection-time validation (broker reachability, STATQ enablement, stats-queue access) is
        // performed lazily by TestConnectionCore and the first GatherThroughput run, where failures
        // are surfaced through Diagnostics and operational logs without preventing the host from
        // starting. Audit-based throughput collection acts as the fallback in those cases.
        var connectionString = settings.TryGetValue(IBMMQSettings.ConnectionString, out var overrideConnection) && !string.IsNullOrWhiteSpace(overrideConnection)
            ? overrideConnection
            : transportSettings.ConnectionString;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            InitialiseErrors.Add("No IBM MQ connection string configured. Set 'IBMMQ/ConnectionString' or configure the Primary instance's transport connection string.");
            Diagnostics.AppendLine("ConnectionString not set");
            return;
        }

        Diagnostics.AppendLine(settings.ContainsKey(IBMMQSettings.ConnectionString)
            ? "ConnectionString set via 'IBMMQ/ConnectionString'"
            : "ConnectionString defaulted to the Primary instance's transport connection string");

        try
        {
            (queueManagerName, connectionProperties) = ConnectionProperties.Parse(connectionString);
        }
        catch (Exception ex)
        {
            InitialiseErrors.Add($"Could not parse IBM MQ connection string: {ex.Message}");
            return;
        }

        statisticsQueueName = settings.TryGetValue(IBMMQSettings.StatisticsQueue, out var statsQueue) && !string.IsNullOrWhiteSpace(statsQueue)
            ? statsQueue
            : IBMMQSettings.DefaultStatisticsQueue;
        Diagnostics.AppendLine($"Statistics queue: {statisticsQueueName}");

        if (settings.TryGetValue(IBMMQSettings.StatisticsForwardingQueue, out var fwdQueue) && !string.IsNullOrWhiteSpace(fwdQueue))
        {
            forwardingQueueName = fwdQueue;
            Diagnostics.AppendLine($"Statistics forwarding queue: {forwardingQueueName}");
        }
        else
        {
            forwardingQueueName = null;
            Diagnostics.AppendLine("Statistics forwarding queue: (not configured)");
        }
    }

    public override async IAsyncEnumerable<IBrokerQueue> GetQueueNames([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ResetCache();
        await PopulateThroughputCacheAsync(cancellationToken).ConfigureAwait(false);

        var queues = await Task.Run(EnumerateUserQueues, cancellationToken).ConfigureAwait(false);
        foreach (var queueName in queues)
        {
            yield return new DefaultBrokerQueue(queueName);
        }
    }

    public override async IAsyncEnumerable<QueueThroughput> GetThroughputPerDay(
        IBrokerQueue brokerQueue,
        DateOnly startDate,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await PopulateThroughputCacheAsync(cancellationToken).ConfigureAwait(false);

        if (!throughputCache.TryGetValue(brokerQueue.QueueName, out var perDay))
        {
            yield break;
        }

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        foreach (var (day, total) in perDay.OrderBy(kv => kv.Key))
        {
            if (day < startDate || day > today)
            {
                continue;
            }
            yield return new QueueThroughput { DateUTC = day, TotalThroughput = total };
        }
    }

    public override KeyDescriptionPair[] Settings =>
    [
        new KeyDescriptionPair(IBMMQSettings.ConnectionString, IBMMQSettings.ConnectionStringDescription),
        new KeyDescriptionPair(IBMMQSettings.StatisticsQueue, IBMMQSettings.StatisticsQueueDescription),
        new KeyDescriptionPair(IBMMQSettings.StatisticsForwardingQueue, IBMMQSettings.StatisticsForwardingQueueDescription)
    ];

    protected override Task<(bool Success, List<string> Errors)> TestConnectionCore(CancellationToken cancellationToken) =>
        Task.Run<(bool, List<string>)>(() =>
        {
            var errors = new List<string>();
            try
            {
                var manager = new MQQueueManager(queueManagerName, connectionProperties);
                try
                {
                    Data["IBMMQVersion"] = manager.CommandLevel.ToString(CultureInfo.InvariantCulture);

                    try
                    {
                        var statsQ = manager.AccessQueue(statisticsQueueName, MQC.MQOO_INQUIRE | MQC.MQOO_FAIL_IF_QUIESCING);
                        statsQ.Close();
                    }
                    catch (Exception ex)
                    {
                        errors.Add(
                            $"Statistics queue '{statisticsQueueName}' is not accessible: {ex.Message}. " +
                            "Verify the queue exists and that the connecting user has +get +inq +browse permissions on it.");
                    }

                    if (!InquireQueueManagerStatistics(manager))
                    {
                        errors.Add(
                            "Statistics collection is disabled on the queue manager (STATQ=OFF). " +
                            "Run `ALTER QMGR STATQ(ON)` to enable broker-side throughput collection. " +
                            "ServiceControl will continue to operate; audit-based throughput will be used as a fallback.");
                    }
                }
                finally
                {
                    try
                    {
                        manager.Disconnect();
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug(ex, "Error disconnecting from queue manager during TestConnection");
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Could not connect to queue manager '{queueManagerName}': {ex.Message}");
            }

            return (errors.Count == 0, errors);
        }, cancellationToken);

    void ResetCache()
    {
        cacheLock.Wait();
        try
        {
            throughputCache.Clear();
            cachePopulated = false;
        }
        finally
        {
            cacheLock.Release();
        }
    }

    async Task PopulateThroughputCacheAsync(CancellationToken cancellationToken)
    {
        if (cachePopulated)
        {
            return;
        }

        await cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (cachePopulated)
            {
                return;
            }
            await Task.Run(DrainStatisticsQueue, cancellationToken).ConfigureAwait(false);
            cachePopulated = true;
        }
        finally
        {
            cacheLock.Release();
        }
    }

    void DrainStatisticsQueue()
    {
        var manager = new MQQueueManager(queueManagerName, connectionProperties);
        MQQueue? statsQueue = null;
        MQQueue? forwardingQueue = null;
        var processed = 0;

        try
        {
            statsQueue = manager.AccessQueue(statisticsQueueName,
                MQC.MQOO_INPUT_AS_Q_DEF | MQC.MQOO_FAIL_IF_QUIESCING);

            if (forwardingQueueName is not null)
            {
                forwardingQueue = manager.AccessQueue(forwardingQueueName,
                    MQC.MQOO_OUTPUT | MQC.MQOO_FAIL_IF_QUIESCING);
            }

            while (true)
            {
                var message = new MQMessage();
                var getOptions = new MQGetMessageOptions
                {
                    Options = MQC.MQGMO_SYNCPOINT | MQC.MQGMO_FAIL_IF_QUIESCING | MQC.MQGMO_NO_WAIT
                };

                try
                {
                    statsQueue.Get(message, getOptions);
                }
                catch (MQException e) when (e.ReasonCode == MQC.MQRC_NO_MSG_AVAILABLE)
                {
                    break;
                }

                ParseAndAggregate(message);

                if (forwardingQueue is not null)
                {
                    var forwardMessage = CloneMessage(message);
                    var putOptions = new MQPutMessageOptions { Options = MQC.MQPMO_SYNCPOINT };
                    forwardingQueue.Put(forwardMessage, putOptions);
                }

                processed++;
            }

            manager.Commit();
            if (processed > 0)
            {
                logger.LogInformation("Drained {Count} IBM MQ statistics messages from {Queue}", processed, statisticsQueueName);
            }
        }
        catch
        {
            try
            {
                manager.Backout();
            }
            catch (Exception backoutEx)
            {
                logger.LogWarning(backoutEx, "Backout of statistics drain failed");
            }
            throw;
        }
        finally
        {
            forwardingQueue?.Close();
            statsQueue?.Close();
            try
            {
                manager.Disconnect();
            }
            catch (Exception disconnectEx)
            {
                logger.LogDebug(disconnectEx, "Error disconnecting from queue manager after statistics drain");
            }
        }
    }

    void ParseAndAggregate(MQMessage message)
    {
        // The IBM .NET PCF API does not implement MQCFGR (group parameter) parsing — its
        // PCFMessage(MQMessage) constructor throws "Unknown type" on any statistics message
        // because per-queue stats are wrapped in MQGACF_Q_STATISTICS_DATA groups. We therefore
        // walk the PCF binary layout directly using the documented MQCFH/MQCFIN/MQCFIN64/MQCFST
        // /MQCFGR struct formats. See IBM MQ "Programmable Command Formats reference" for the
        // wire layout.
        try
        {
            ParseStatisticsMessage(message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Skipping malformed PCF message on {Queue}", statisticsQueueName);
        }
    }

    void ParseStatisticsMessage(MQMessage message)
    {
        message.Seek(0);

        // MQCFH header: Type, StrucLength, Version, Command, MsgSeqNumber, Control, CompCode, Reason, ParameterCount.
        var headerType = message.ReadInt4();
        var headerStrucLength = message.ReadInt4();
        message.SkipBytes(4); // Version
        var headerCommand = message.ReadInt4();
        message.SkipBytes(16); // MsgSeqNumber, Control, CompCode, Reason
        var paramsRemaining = message.ReadInt4();

        if (headerStrucLength > 36)
        {
            message.SkipBytes(headerStrucLength - 36);
        }

        if (headerType != MQC.MQCFT_STATISTICS || headerCommand != MQC.MQCMD_STATISTICS_Q)
        {
            return;
        }

        DateOnly? endDate = null;
        string? currentQueue = null;

        while (paramsRemaining > 0)
        {
            paramsRemaining--;

            var paramType = message.ReadInt4();
            var paramStrucLength = message.ReadInt4();
            var bytesConsumed = 8;

            switch (paramType)
            {
                case MQC.MQCFT_GROUP:
                    {
                        var groupParam = message.ReadInt4();
                        var groupParamCount = message.ReadInt4();
                        bytesConsumed += 8;
                        if (groupParam == MQGACF_Q_STATISTICS_DATA)
                        {
                            currentQueue = null;
                        }
                        paramsRemaining += groupParamCount;
                        break;
                    }
                case MQC.MQCFT_INTEGER:
                    {
                        var paramId = message.ReadInt4();
                        var value = message.ReadInt4();
                        bytesConsumed += 8;
                        HandleInt(paramId, value, currentQueue, endDate);
                        break;
                    }
                case MQC.MQCFT_INTEGER64:
                    {
                        var paramId = message.ReadInt4();
                        message.SkipBytes(4); // reserved
                        var value = message.ReadInt8();
                        bytesConsumed += 16;
                        HandleInt(paramId, value, currentQueue, endDate);
                        break;
                    }
                case MQC.MQCFT_INTEGER_LIST:
                    {
                        var paramId = message.ReadInt4();
                        var count = message.ReadInt4();
                        bytesConsumed += 8;
                        long sum = 0;
                        for (var i = 0; i < count; i++)
                        {
                            sum += message.ReadInt4();
                            bytesConsumed += 4;
                        }
                        HandleInt(paramId, sum, currentQueue, endDate);
                        break;
                    }
                case MQC.MQCFT_INTEGER64_LIST:
                    {
                        var paramId = message.ReadInt4();
                        var count = message.ReadInt4();
                        bytesConsumed += 8;
                        long sum = 0;
                        for (var i = 0; i < count; i++)
                        {
                            sum += message.ReadInt8();
                            bytesConsumed += 8;
                        }
                        HandleInt(paramId, sum, currentQueue, endDate);
                        break;
                    }
                case MQC.MQCFT_STRING:
                    {
                        var paramId = message.ReadInt4();
                        message.SkipBytes(4); // CodedCharSetId
                        var stringLength = message.ReadInt4();
                        bytesConsumed += 12;
                        var raw = message.ReadString(stringLength);
                        bytesConsumed += stringLength;
                        var trimmed = raw.TrimEnd();
                        if (paramId == MQC.MQCA_Q_NAME)
                        {
                            currentQueue = trimmed;
                        }
                        else if (paramId == MQC.MQCAMO_END_DATE)
                        {
                            endDate = TryParseStatisticsDate(trimmed);
                        }
                        break;
                    }
                default:
                    // Unknown / uninteresting parameter — skip the rest of its bytes.
                    break;
            }

            if (paramStrucLength > bytesConsumed)
            {
                message.SkipBytes(paramStrucLength - bytesConsumed);
            }
        }

        void HandleInt(int paramId, long value, string? queue, DateOnly? day)
        {
            if (paramId == MQC.MQIAMO_GETS && !string.IsNullOrEmpty(queue) && day.HasValue)
            {
                AddToCache(queue, day.Value, value);
            }
        }
    }

    static DateOnly? TryParseStatisticsDate(string raw)
    {
        if (DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
        {
            return d;
        }
        return null;
    }

    // The .NET MQC class does not expose group-parameter constants. The numeric value is
    // taken from IBM MQ's CMQCFC.h: MQGACF_Q_STATISTICS_DATA = 8003.
    const int MQGACF_Q_STATISTICS_DATA = 8003;

    void AddToCache(string queueName, DateOnly day, long gets)
    {
        if (queueName.StartsWith("SYSTEM.", StringComparison.Ordinal))
        {
            return;
        }

        if (!throughputCache.TryGetValue(queueName, out var perDay))
        {
            perDay = [];
            throughputCache[queueName] = perDay;
        }

        perDay[day] = perDay.GetValueOrDefault(day, 0L) + gets;
    }

    List<string> EnumerateUserQueues()
    {
        var manager = new MQQueueManager(queueManagerName, connectionProperties);
        var agent = new PCFMessageAgent(manager);
        try
        {
            var request = new PCFMessage(MQC.MQCMD_INQUIRE_Q);
            request.AddParameter(MQC.MQCA_Q_NAME, "*");
            request.AddParameter(MQC.MQIA_Q_TYPE, MQC.MQQT_LOCAL);
            var responses = agent.Send(request);

            var queues = new List<string>(responses.Length);
            foreach (var response in responses)
            {
                string name;
                try
                {
                    name = response.GetStringParameterValue(MQC.MQCA_Q_NAME).TrimEnd();
                }
                catch
                {
                    continue;
                }

                if (string.IsNullOrEmpty(name) || name.StartsWith("SYSTEM.", StringComparison.Ordinal))
                {
                    continue;
                }

                queues.Add(name);
            }
            return queues;
        }
        finally
        {
            try
            {
                agent.Disconnect();
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Error disconnecting PCF agent after queue enumeration");
            }
        }
    }

    bool InquireQueueManagerStatistics(MQQueueManager manager)
    {
        var agent = new PCFMessageAgent(manager);
        try
        {
            var request = new PCFMessage(MQC.MQCMD_INQUIRE_Q_MGR);
            request.AddParameter(MQC.MQIACF_Q_MGR_ATTRS, new[] { MQC.MQIA_STATISTICS_Q });
            var responses = agent.Send(request);

            foreach (var response in responses)
            {
                try
                {
                    var value = response.GetIntParameterValue(MQC.MQIA_STATISTICS_Q);
                    return value == MQC.MQMON_ON;
                }
                catch
                {
                    continue;
                }
            }
            return false;
        }
        finally
        {
            try
            {
                agent.Disconnect();
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Error disconnecting PCF agent after queue-manager attribute query");
            }
        }
    }

    static MQMessage CloneMessage(MQMessage source)
    {
        var clone = new MQMessage
        {
            Format = source.Format,
            CharacterSet = source.CharacterSet,
            Encoding = source.Encoding
        };
        source.Seek(0);
        var bytes = source.ReadBytes(source.MessageLength);
        clone.Write(bytes);
        return clone;
    }
}