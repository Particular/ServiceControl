namespace NServiceBus.Transport.Msmq
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using System.Transactions;
    using DelayedDelivery;
    using Faults;
    using MSMQ.Messaging;
    using NServiceBus.DelayedDelivery;
    using Performance.TimeToBeReceived;
    using Routing;
    using Settings;
    using Support;
    using Transport;

    class MsmqTransportInfrastructure : TransportInfrastructure
    {
        const string TimeoutQueueSuffix = "timeouts";

        public MsmqTransportInfrastructure(ReadOnlySettings settings, MsmqSettings msmqSettings, QueueBindings queueBindings, bool isTransactional, bool outBoxRunning, TimeSpan auditMessageExpiration, Func<LogicalAddress> localAddress, string timeoutsErrorQueue)
        {
            this.settings = settings;
            this.msmqSettings = msmqSettings;
            this.queueBindings = queueBindings;
            this.isTransactional = isTransactional;
            this.outBoxRunning = outBoxRunning;
            this.auditMessageExpiration = auditMessageExpiration;
            this.localAddress = localAddress;
            this.timeoutsErrorQueue = timeoutsErrorQueue;

            if (msmqSettings.UseNativeDelayedDelivery)
            {
                constraints = new[]
                {
                    typeof(DiscardIfNotReceivedBefore),
                    typeof(NonDurableDelivery),
                    typeof(DelayDeliveryWith),
                    typeof(DoNotDeliverBefore)
                };
            }
            else
            {
                constraints = new[]
                {
                    typeof(DiscardIfNotReceivedBefore),
                    typeof(NonDurableDelivery)
                };
            }
        }

        public override IEnumerable<Type> DeliveryConstraints => constraints;

        public override TransportTransactionMode TransactionMode { get; } = TransportTransactionMode.TransactionScope;
        public override OutboundRoutingPolicy OutboundRoutingPolicy { get; } = new OutboundRoutingPolicy(OutboundRoutingType.Unicast, OutboundRoutingType.Unicast, OutboundRoutingType.Unicast);

        ReceiveStrategy SelectReceiveStrategy(TransportTransactionMode minimumConsistencyGuarantee, TransactionOptions transactionOptions)
        {
            switch (minimumConsistencyGuarantee)
            {
                case TransportTransactionMode.TransactionScope:
                    return new TransactionScopeStrategy(transactionOptions, new MsmqFailureInfoStorage(1000));
                case TransportTransactionMode.SendsAtomicWithReceive:
                    return new SendsAtomicWithReceiveNativeTransactionStrategy(new MsmqFailureInfoStorage(1000));
                case TransportTransactionMode.ReceiveOnly:
                    return new ReceiveOnlyNativeTransactionStrategy(new MsmqFailureInfoStorage(1000));
                case TransportTransactionMode.None:
                    return new NoTransactionStrategy();
                default:
                    throw new NotSupportedException($"TransportTransactionMode {minimumConsistencyGuarantee} is not supported by the MSMQ transport");
            }
        }

        public override EndpointInstance BindToLocalEndpoint(EndpointInstance instance) => instance.AtMachine(RuntimeEnvironment.MachineName);

        public override string ToTransportAddress(LogicalAddress logicalAddress)
        {
            if (!logicalAddress.EndpointInstance.Properties.TryGetValue("machine", out var machine))
            {
                machine = RuntimeEnvironment.MachineName;
            }
            if (!logicalAddress.EndpointInstance.Properties.TryGetValue("queue", out var queueName))
            {
                queueName = logicalAddress.EndpointInstance.Endpoint;
            }
            var queue = new StringBuilder(queueName);
            if (logicalAddress.EndpointInstance.Discriminator != null)
            {
                queue.Append("-" + logicalAddress.EndpointInstance.Discriminator);
            }
            if (logicalAddress.Qualifier != null)
            {
                queue.Append("." + logicalAddress.Qualifier);
            }
            return $"{queue}@{machine}";
        }

        public override string MakeCanonicalForm(string transportAddress)
        {
            return MsmqAddress.Parse(transportAddress).ToString();
        }

        public override TransportReceiveInfrastructure ConfigureReceiveInfrastructure()
        {
            CheckMachineNameForCompliance.Check();

            IPushMessages delayedDeliveryPump = null;

            string timeoutsQueue = null;
            string timeoutsErrorQueue = null;
            if (msmqSettings.UseNativeDelayedDelivery)
            {
                timeoutsQueue = ToTransportAddress(localAddress().CreateQualifiedAddress(TimeoutQueueSuffix));
                timeoutsErrorQueue = this.timeoutsErrorQueue;

                var dispatcher = new MsmqMessageDispatcher(msmqSettings, timeoutsQueue);

                var staticFaultMetadata = new Dictionary<string, string>
                {
                    {FaultsHeaderKeys.FailedQ, timeoutsQueue},
                    {Headers.ProcessingMachine, RuntimeEnvironment.MachineName},
                    {Headers.ProcessingEndpoint, localAddress().EndpointInstance.Endpoint},
                };

                var dueDelayedMessagePoller = new DueDelayedMessagePoller(dispatcher, msmqSettings.NativeDelayedDeliverySettings.DelayedMessageStore, msmqSettings.NativeDelayedDeliverySettings.NumberOfRetries, timeoutsErrorQueue, staticFaultMetadata,
                    msmqSettings.NativeDelayedDeliverySettings.TimeToTriggerFetchCircuitBreaker,
                    msmqSettings.NativeDelayedDeliverySettings.TimeToTriggerDispatchCircuitBreaker,
                    msmqSettings.NativeDelayedDeliverySettings.MaximumRecoveryFailuresPerSecond);

                var delayedDeliveryMessagePump = new MessagePump(guarantee => SelectReceiveStrategy(guarantee, msmqSettings.ScopeOptions.TransactionOptions), msmqSettings.MessageEnumeratorTimeout, false);

                delayedDeliveryPump = new DelayedDeliveryPump(dispatcher, dueDelayedMessagePoller, msmqSettings.NativeDelayedDeliverySettings.DelayedMessageStore, delayedDeliveryMessagePump, timeoutsQueue, timeoutsErrorQueue, msmqSettings.NativeDelayedDeliverySettings.NumberOfRetries, msmqSettings.NativeDelayedDeliverySettings.TimeToTriggerStoreCircuitBreaker, staticFaultMetadata);
            }

            // The following check avoids creating some sub-queues, if the endpoint sub queue has the capability to exceed the max length limitation for queue format name.
            foreach (var queue in queueBindings.ReceivingAddresses)
            {
                CheckEndpointNameComplianceForMsmq.Check(queue);
            }

            return new TransportReceiveInfrastructure(
                () => new CompositePump(new MessagePump(guarantee => SelectReceiveStrategy(guarantee, msmqSettings.ScopeOptions.TransactionOptions), msmqSettings.MessageEnumeratorTimeout, !msmqSettings.IgnoreIncomingTimeToBeReceivedHeaders), delayedDeliveryPump),
                () =>
                {
                    if (msmqSettings.ExecuteInstaller)
                    {
                        string endpointName = null;
                        if (msmqSettings.NativeDelayedDeliverySettings != null)
                        {
                            endpointName = localAddress().EndpointInstance.Endpoint;
                        }

                        return new MsmqQueueCreator(msmqSettings.UseTransactionalQueues, msmqSettings.NativeDelayedDeliverySettings?.DelayedMessageStore, endpointName, timeoutsQueue, timeoutsErrorQueue);
                    }
                    return new NullQueueCreator();
                },
                () =>
                {
                    foreach (var address in queueBindings.ReceivingAddresses)
                    {
                        QueuePermissions.CheckQueue(address);
                    }
                    return Task.FromResult(StartupCheckResult.Success);
                });
        }

        public override TransportSendInfrastructure ConfigureSendInfrastructure()
        {
            CheckMachineNameForCompliance.Check();

            string timeoutsQueue = null;
            if (msmqSettings.UseNativeDelayedDelivery)
            {
                timeoutsQueue = ToTransportAddress(localAddress().CreateQualifiedAddress(TimeoutQueueSuffix));
            }

            return new TransportSendInfrastructure(
                () => new MsmqMessageDispatcher(msmqSettings, timeoutsQueue),
                () =>
                {
                    foreach (var address in queueBindings.SendingAddresses)
                    {
                        QueuePermissions.CheckQueue(address);
                    }

                    var auditTTBROverridden = auditMessageExpiration > TimeSpan.Zero;
                    var result = TimeToBeReceivedOverrideChecker.Check(isTransactional, outBoxRunning, auditTTBROverridden);
                    return Task.FromResult(result);
                });
        }

        public override Task Start()
        {
            settings.AddStartupDiagnosticsSection("NServiceBus.Transport.MSMQ", new
            {
                msmqSettings.ExecuteInstaller,
                msmqSettings.UseDeadLetterQueue,
                msmqSettings.UseConnectionCache,
                msmqSettings.UseTransactionalQueues,
                msmqSettings.UseJournalQueue,
                msmqSettings.UseDeadLetterQueueForMessagesWithTimeToBeReceived,
                TimeToReachQueue = GetFormattedTimeToReachQueue(msmqSettings.TimeToReachQueue)
            });

            return Task.FromResult(0);
        }

        static string GetFormattedTimeToReachQueue(TimeSpan timeToReachQueue)
        {
            return timeToReachQueue == Message.InfiniteTimeout ? "Infinite"
                : string.Format("{0:%d} day(s) {0:%hh} hours(s) {0:%mm} minute(s) {0:%ss} second(s)", timeToReachQueue);
        }



        public override TransportSubscriptionInfrastructure ConfigureSubscriptionInfrastructure()
        {
            throw new NotImplementedException("MSMQ does not support native pub/sub.");
        }

        ReadOnlySettings settings;
        MsmqSettings msmqSettings;
        QueueBindings queueBindings;
        bool isTransactional;
        bool outBoxRunning;
        TimeSpan auditMessageExpiration;
        Func<LogicalAddress> localAddress;
        string timeoutsErrorQueue;
        Type[] constraints;
    }
}