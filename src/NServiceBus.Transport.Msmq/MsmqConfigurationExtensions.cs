namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Messaging;
    using System.Transactions;
    using Configuration.AdvancedExtensibility;
    using Routing;
    using Transport.Msmq;

    /// <summary>
    /// Adds extensions methods to <see cref="TransportExtensions{T}" /> for configuration purposes.
    /// </summary>
    public static class MsmqConfigurationExtensions
    {
        /// <summary>
        /// Set a delegate to use for applying the <see cref="Message.Label" /> property when sending a message.
        /// </summary>
        /// <remarks>
        /// This delegate will be used for all valid messages sent via MSMQ.
        /// This includes, not just standard messages, but also Audits, Errors and all control messages.
        /// In some cases it may be useful to use the <see cref="Headers.ControlMessageHeader" /> key to determine if a message is
        /// a control message.
        /// The only exception to this rule is received messages with corrupted headers. These messages will be forwarded to the
        /// error queue with no label applied.
        /// </remarks>
        public static TransportExtensions<MsmqTransport> ApplyLabelToMessages(this TransportExtensions<MsmqTransport> transportExtensions, Func<IReadOnlyDictionary<string, string>, string> labelGenerator)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);
            Guard.AgainstNull(nameof(labelGenerator), labelGenerator);
            transportExtensions.GetSettings().Set("msmqLabelGenerator", labelGenerator);
            return transportExtensions;
        }

        /// <summary>
        /// Allows to change the transaction isolation level and timeout for the `TransactionScope` used to receive messages.
        /// </summary>
        /// <remarks>
        /// If not specified the default transaction timeout of the machine will be used and the isolation level will be set to
        /// <see cref="IsolationLevel.ReadCommitted"/>.
        /// </remarks>
        /// <param name="transportExtensions">MSMQ Transport configuration object.</param>
        /// <param name="timeout">Transaction timeout duration.</param>
        /// <param name="isolationLevel">Transaction isolation level.</param>
        public static TransportExtensions<MsmqTransport> TransactionScopeOptions(this TransportExtensions<MsmqTransport> transportExtensions, TimeSpan? timeout = null, IsolationLevel? isolationLevel = null)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);
            Guard.AgainstNegativeAndZero(nameof(timeout), timeout);

            if (isolationLevel.HasValue && isolationLevel.Value == IsolationLevel.Snapshot)
            {
                throw new ArgumentException("Isolation level `Snapshot` is not supported by the transport. Consider not sharing the transaction between transport and persistence if persistence should use `IsolationLevel.Snapshot` by using `TransportTransactionMode.SendsAtomicWithReceive` or lower.", nameof(isolationLevel));
            }

            transportExtensions.GetSettings().Set<MsmqScopeOptions>(new MsmqScopeOptions(timeout, isolationLevel));
            return transportExtensions;
        }

        /// <summary>
        /// Sets a distribution strategy for a given endpoint.
        /// </summary>
        /// <param name="config">MSMQ Transport configuration object.</param>
        /// <param name="distributionStrategy">The instance of a distribution strategy.</param>
        public static void SetMessageDistributionStrategy(this RoutingSettings<MsmqTransport> config, DistributionStrategy distributionStrategy)
        {
            Guard.AgainstNull(nameof(config), config);
            Guard.AgainstNull(nameof(distributionStrategy), distributionStrategy);
            config.GetSettings().GetOrCreate<List<DistributionStrategy>>().Add(distributionStrategy);
        }

        /// <summary>
        /// Returns the configuration options for the file based instance mapping file.
        /// </summary>
        /// <param name="config">MSMQ Transport configuration object.</param>
        public static InstanceMappingFileSettings InstanceMappingFile(this RoutingSettings<MsmqTransport> config)
        {
            Guard.AgainstNull(nameof(config), config);
            return new InstanceMappingFileSettings(config.GetSettings());
        }

        /// <summary>
        /// Moves messages that have exceeded their TimeToBeReceived to the dead letter queue instead of discarding them.
        /// </summary>
        /// <param name="config">MSMQ Transport configuration object.</param>
        public static void UseDeadLetterQueueForMessagesWithTimeToBeReceived(this TransportExtensions<MsmqTransport> config)
        {
            Guard.AgainstNull(nameof(config), config);
            config.GetSettings().Set("UseDeadLetterQueueForMessagesWithTimeToBeReceived", true);
        }

        /// <summary>
        /// Disables the automatic queue creation when installers are enabled using `EndpointConfiguration.EnableInstallers()`.
        /// </summary>
        /// <remarks>
        /// With installers enabled, required queues will be created automatically at startup.While this may be convenient for development,
        /// we instead recommend that queues are created as part of deployment using the CreateQueues.ps1 script included in the NuGet package.
        /// The installers might still need to be enabled to fulfill the installation needs of other components, but this method allows
        /// scripts to be used for queue creation instead.
        /// </remarks>
        /// <param name="config">MSMQ Transport configuration object.</param>
        public static void DisableInstaller(this TransportExtensions<MsmqTransport> config)
        {
            Guard.AgainstNull(nameof(config), config);
            config.GetSettings().Set("ExecuteInstaller", false);
        }

        /// <summary>
        /// This setting should be used with caution. It disables the storing of undeliverable messages
        /// in the dead letter queue. Therefore this setting must only be used where loss of messages 
        /// is an acceptable scenario. 
        /// </summary>
        /// <param name="config">MSMQ Transport configuration object.</param>
        public static void DisableDeadLetterQueueing(this TransportExtensions<MsmqTransport> config)
        {
            Guard.AgainstNull(nameof(config), config);
            config.GetSettings().Set("UseDeadLetterQueue", false);
        }

        /// <summary>
        /// Instructs MSMQ to cache connections to a remote queue and re-use them
        /// as needed instead of creating new connections for each message. 
        /// Turning connection caching off will negatively impact the message throughput in 
        /// most scenarios.
        /// </summary>
        /// <param name="config">MSMQ Transport configuration object.</param>
        public static void DisableConnectionCachingForSends(this TransportExtensions<MsmqTransport> config)
        {
            Guard.AgainstNull(nameof(config), config);
            config.GetSettings().Set("UseConnectionCache", false);
        }

        /// <summary>
        /// This setting should be used with caution. As the queues are not transactional, any message that has
        /// an exception during processing will not be rolled back to the queue. Therefore this setting must only
        /// be used where loss of messages is an acceptable scenario.  
        /// </summary>
        /// <param name="config">MSMQ Transport configuration object.</param>
        public static void UseNonTransactionalQueues(this TransportExtensions<MsmqTransport> config)
        {
            Guard.AgainstNull(nameof(config), config);
            config.GetSettings().Set("UseTransactionalQueues", false);
        }

        /// <summary>
        /// Enables the use of journaling messages. Stores a copy of every message received in the journal queue. 
        /// Should be used ONLY when debugging as it can 
        /// potentially use up the MSMQ journal storage quota based on the message volume.
        /// </summary>
        /// <param name="config">MSMQ Transport configuration object.</param>
        public static void EnableJournaling(this TransportExtensions<MsmqTransport> config)
        {
            Guard.AgainstNull(nameof(config), config);
            config.GetSettings().Set("UseJournalQueue", true);
        }

        /// <summary>
        /// Overrides the Time-To-Reach-Queue (TTRQ) timespan. The default value if not set is Message.InfiniteTimeout
        /// </summary>
        /// <param name="config">MSMQ Transport configuration object.</param>
        /// <param name="timeToReachQueue">Timespan for the Time-To-Reach-Queue (TTRQ)</param>
        public static void TimeToReachQueue(this TransportExtensions<MsmqTransport> config, TimeSpan timeToReachQueue)
        {
            Guard.AgainstNull(nameof(config), config);
            Guard.AgainstNegativeAndZero(nameof(timeToReachQueue), timeToReachQueue);
            config.GetSettings().Set("TimeToReachQueue", timeToReachQueue);
        }

        /// <summary>
        /// Disables native Time-To-Be-Received (TTBR) when combined with transactions.
        /// </summary>
        /// <param name="config">MSMQ Transport configuration object.</param>
        public static void DisableNativeTimeToBeReceivedInTransactions(this TransportExtensions<MsmqTransport> config)
        {
            Guard.AgainstNull(nameof(config), config);
            config.GetSettings().Set("DisableNativeTtbrInTransactions", true);
        }

        /// <summary>
        /// Ignore incoming Time-To-Be-Received (TTBR) headers. By default an expired TTBR header will result in the message to be discarded.
        /// </summary>
        /// <param name="config">MSMQ Transport configuration object.</param>
        public static void IgnoreIncomingTimeToBeReceivedHeaders(this TransportExtensions<MsmqTransport> config)
        {
            Guard.AgainstNull(nameof(config), config);
            config.GetSettings().Set("IgnoreIncomingTimeToBeReceivedHeaders", true);
        }

        /// <summary>
        /// Configures native delayed delivery.
        /// </summary>
        public static DelayedDeliverySettings NativeDelayedDelivery(this TransportExtensions<MsmqTransport> config, IDelayedMessageStore delayedMessageStore)
        {
            var sendOnlyEndpoint = config.GetSettings().GetOrDefault<bool>("Endpoint.SendOnly");
            if (sendOnlyEndpoint)
            {
                throw new Exception("Delayed delivery is only supported for endpoints capable of receiving messages.");
            }

            //Enable hybrid mode in which timeout manager is still running in the core to process remaining timeouts.
            config.GetSettings().Set("NServiceBus.TimeoutManager.EnableMigrationMode", true);

            var delayedDeliverySettings = config.GetSettings().GetOrCreate<DelayedDeliverySettings>();
            delayedDeliverySettings.DelayedMessageStore = delayedMessageStore;

            return delayedDeliverySettings;
        }
    }
}
