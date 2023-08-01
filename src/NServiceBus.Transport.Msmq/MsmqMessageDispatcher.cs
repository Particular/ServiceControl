namespace NServiceBus.Transport.Msmq
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Messaging;
    using System.Threading.Tasks;
    using System.Transactions;
    using DeliveryConstraints;
    using Extensibility;
    using Performance.TimeToBeReceived;
    using Transport;
    using Unicast.Queuing;
    using NServiceBus.DelayedDelivery;

    class MsmqMessageDispatcher : IDispatchMessages
    {
        public MsmqMessageDispatcher(MsmqSettings settings, string timeoutsQueue)
        {
            this.settings = settings;
            this.timeoutsQueue = timeoutsQueue;
        }

        public Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, ContextBag context)
        {
            Guard.AgainstNull(nameof(outgoingMessages), outgoingMessages);

            if (outgoingMessages.MulticastTransportOperations.Any())
            {
                throw new Exception("The MSMQ transport only supports unicast transport operations.");
            }

            foreach (var unicastTransportOperation in outgoingMessages.UnicastTransportOperations)
            {
                ExecuteTransportOperation(transaction, unicastTransportOperation, context);
            }

            return TaskEx.CompletedTask;
        }

        public void DispatchDelayedMessage(string id, byte[] extension, byte[] body, string destination, TransportTransaction transportTransaction, ContextBag context)
        {
            var headers = MsmqUtilities.DeserializeMessageHeaders(extension);

            if (headers.TryGetValue(MsmqUtilities.PropertyHeaderPrefix + DeadLetterQueueOptionExtensions.KeyDeadLetterQueue, out var deadLetterQueue))
            {
                context.Set(DeadLetterQueueOptionExtensions.KeyDeadLetterQueue, bool.Parse(deadLetterQueue));
            }
            if (headers.TryGetValue(MsmqUtilities.PropertyHeaderPrefix + JournalOptionExtensions.KeyJournaling, out var useJournalQueue))
            {
                context.Set(JournalOptionExtensions.KeyJournaling, bool.Parse(useJournalQueue));
            }

            headers.Remove(MsmqUtilities.PropertyHeaderPrefix + TimeoutDestination);
            headers.Remove(MsmqUtilities.PropertyHeaderPrefix + TimeoutAt);
            headers.Remove(MsmqUtilities.PropertyHeaderPrefix + DeadLetterQueueOptionExtensions.KeyDeadLetterQueue);
            headers.Remove(MsmqUtilities.PropertyHeaderPrefix + JournalOptionExtensions.KeyJournaling);

            var request = new OutgoingMessage(id, headers, body);

            SendToDestination(transportTransaction, new UnicastTransportOperation(request, destination), context);
        }

        void ExecuteTransportOperation(TransportTransaction transaction, UnicastTransportOperation transportOperation, ContextBag context)
        {
            DateTimeOffset? deliverAt = null;
            if (timeoutsQueue != null && context.TryGetDeliveryConstraint<DoNotDeliverBefore>(out var doNotDeliverBefore))
            {
                deliverAt = doNotDeliverBefore.At;
            }
            else if (timeoutsQueue != null && context.TryGetDeliveryConstraint<DelayDeliveryWith>(out var delayDeliveryWith))
            {
                deliverAt = DateTimeOffset.UtcNow + delayDeliveryWith.Delay;
            }

            if (deliverAt.HasValue)
            {
                SendToDelayedDeliveryQueue(transaction, transportOperation, context, deliverAt.Value);
            }
            else
            {
                SendToDestination(transaction, transportOperation, context);
            }
        }

        void SendToDelayedDeliveryQueue(TransportTransaction transaction, UnicastTransportOperation transportOperation, ContextBag context, DateTimeOffset deliverAt)
        {
            var message = transportOperation.Message;
            var headers = message.Headers;
            var destinationAddress = MsmqAddress.Parse(timeoutsQueue);


            headers[MsmqUtilities.PropertyHeaderPrefix + TimeoutDestination] = transportOperation.Destination;
            headers[MsmqUtilities.PropertyHeaderPrefix + TimeoutAt] = DateTimeOffsetHelper.ToWireFormattedString(deliverAt);

            var operationProperties = context.GetOperationProperties();
            if (operationProperties.TryGet<bool>(DeadLetterQueueOptionExtensions.KeyDeadLetterQueue, out var useDeadLetterQueue))
            {
                headers[MsmqUtilities.PropertyHeaderPrefix + DeadLetterQueueOptionExtensions.KeyDeadLetterQueue] = useDeadLetterQueue.ToString();
            }
            if (operationProperties.TryGet<bool>(JournalOptionExtensions.KeyJournaling, out var useJournalQueue))
            {
                headers[MsmqUtilities.PropertyHeaderPrefix + JournalOptionExtensions.KeyJournaling] = useJournalQueue.ToString();
            }

            try
            {
                using (var q = new MessageQueue(destinationAddress.FullPath, false, settings.UseConnectionCache, QueueAccessMode.Send))
                {
                    using (var toSend = MsmqUtilities.Convert(message, transportOperation.DeliveryConstraints))
                    {
                        toSend.UseDeadLetterQueue = true; // Always used DLQ for sending delayed messages to the satellite
                        toSend.UseJournalQueue = settings.UseJournalQueue;

                        if (transportOperation.RequiredDispatchConsistency == DispatchConsistency.Isolated)
                        {
                            q.Send(toSend, string.Empty, GetIsolatedTransactionType());
                            return;
                        }

                        if (TryGetNativeTransaction(transaction, out var activeTransaction))
                        {
                            q.Send(toSend, string.Empty, activeTransaction);
                            return;
                        }

                        q.Send(toSend, string.Empty, GetTransactionTypeForSend());
                    }
                }
            }
            catch (MessageQueueException ex)
            {
                if (ex.MessageQueueErrorCode == MessageQueueErrorCode.QueueNotFound)
                {
                    throw new QueueNotFoundException(timeoutsQueue, $"Failed to send the message to the local delayed delivery queue [{timeoutsQueue}]: queue does not exist.", ex);
                }

                ThrowFailedToSendException(timeoutsQueue, ex);
            }
            catch (Exception ex)
            {
                ThrowFailedToSendException(timeoutsQueue, ex);
            }
        }

        void SendToDestination(TransportTransaction transaction, UnicastTransportOperation transportOperation, ContextBag context)
        {
            var message = transportOperation.Message;

            var destination = transportOperation.Destination;
            var destinationAddress = MsmqAddress.Parse(destination);

            var deliveryConstraints = transportOperation.DeliveryConstraints;

            if (IsCombiningTimeToBeReceivedWithTransactions(
                transaction,
                transportOperation.RequiredDispatchConsistency,
                transportOperation.DeliveryConstraints))
            {
                if (settings.DisableNativeTtbrInTransactions)
                {
                    deliveryConstraints = deliveryConstraints.Except(deliveryConstraints.OfType<DiscardIfNotReceivedBefore>()).ToList();
                }
                else
                {
                    throw new Exception($"Failed to send message to address: {destinationAddress.Queue}@{destinationAddress.Machine}. Sending messages with a custom TimeToBeReceived is not supported on transactional MSMQ.");
                }
            }
            try
            {
                using (var q = new MessageQueue(destinationAddress.FullPath, false, settings.UseConnectionCache, QueueAccessMode.Send))
                {
                    using (var toSend = MsmqUtilities.Convert(message, deliveryConstraints))
                    {
                        var operationProperties = context.GetOperationProperties();
                        // or check headers for NServiceBus.Timeouts.Properties.DeadLetterQueueOptionExtensions.KeyDeadLetterQueue
                        if (operationProperties.TryGet<bool>(DeadLetterQueueOptionExtensions.KeyDeadLetterQueue, out var useDeadLetterQueue))
                        {
                            toSend.UseDeadLetterQueue = useDeadLetterQueue;
                        }
                        else
                        {
                            var ttbrRequested = toSend.TimeToBeReceived < MessageQueue.InfiniteTimeout;
                            toSend.UseDeadLetterQueue = ttbrRequested
                                ? settings.UseDeadLetterQueueForMessagesWithTimeToBeReceived
                                : settings.UseDeadLetterQueue;
                        }

                        // or check headers for NServiceBus.Timeouts.Properties.JournalOptionExtensions.KeyJournaling
                        toSend.UseJournalQueue = operationProperties.TryGet<bool>(JournalOptionExtensions.KeyJournaling, out var useJournalQueue)
                            ? useJournalQueue
                            : settings.UseJournalQueue;

                        toSend.TimeToReachQueue = settings.TimeToReachQueue;

                        if (message.Headers.TryGetValue(Headers.ReplyToAddress, out var replyToAddress))
                        {
                            toSend.ResponseQueue = new MessageQueue(MsmqAddress.Parse(replyToAddress).FullPath);
                        }

                        var label = GetLabel(message);

                        if (transportOperation.RequiredDispatchConsistency == DispatchConsistency.Isolated)
                        {
                            q.Send(toSend, label, GetIsolatedTransactionType());
                            return;
                        }

                        if (TryGetNativeTransaction(transaction, out var activeTransaction))
                        {
                            q.Send(toSend, label, activeTransaction);
                            return;
                        }

                        q.Send(toSend, label, GetTransactionTypeForSend());
                    }
                }
            }
            catch (MessageQueueException ex)
            {
                if (ex.MessageQueueErrorCode == MessageQueueErrorCode.QueueNotFound)
                {
                    var msg = destination == null
                        ? "Failed to send message. Target address is null."
                        : $"Failed to send message to address: [{destination}]";

                    throw new QueueNotFoundException(destination, msg, ex);
                }

                ThrowFailedToSendException(destination, ex);
            }
            catch (Exception ex)
            {
                ThrowFailedToSendException(destination, ex);
            }
        }

        bool IsCombiningTimeToBeReceivedWithTransactions(TransportTransaction transaction, DispatchConsistency requiredDispatchConsistency, List<DeliveryConstraint> deliveryConstraints)
        {
            if (!settings.UseTransactionalQueues)
            {
                return false;
            }

            if (requiredDispatchConsistency == DispatchConsistency.Isolated)
            {
                return false;
            }

            var timeToBeReceivedRequested = deliveryConstraints.TryGet(out DiscardIfNotReceivedBefore discardIfNotReceivedBefore) && discardIfNotReceivedBefore.MaxTime < MessageQueue.InfiniteTimeout;

            if (!timeToBeReceivedRequested)
            {
                return false;
            }

            if (Transaction.Current != null)
            {
                return true;
            }


            return TryGetNativeTransaction(transaction, out _);
        }

        static bool TryGetNativeTransaction(TransportTransaction transportTransaction, out MessageQueueTransaction transaction)
        {
            return transportTransaction.TryGet(out transaction);
        }

        MessageQueueTransactionType GetIsolatedTransactionType()
        {
            return settings.UseTransactionalQueues ? MessageQueueTransactionType.Single : MessageQueueTransactionType.None;
        }

        string GetLabel(OutgoingMessage message)
        {
            var messageLabel = settings.LabelGenerator(new ReadOnlyDictionary<string, string>(message.Headers));
            if (messageLabel == null)
            {
                throw new Exception("MSMQ label convention returned a null. Either return a valid value or a String.Empty to indicate 'no value'.");
            }
            if (messageLabel.Length > 240)
            {
                throw new Exception("MSMQ label convention returned a value longer than 240 characters. This is not supported.");
            }
            return messageLabel;
        }

        static void ThrowFailedToSendException(string address, Exception ex)
        {
            if (address == null)
            {
                throw new Exception("Failed to send message.", ex);
            }

            throw new Exception($"Failed to send message to address: {address}", ex);
        }

        MessageQueueTransactionType GetTransactionTypeForSend()
        {
            if (!settings.UseTransactionalQueues)
            {
                return MessageQueueTransactionType.None;
            }

            return Transaction.Current != null
                ? MessageQueueTransactionType.Automatic
                : MessageQueueTransactionType.Single;
        }

        public const string TimeoutDestination = "NServiceBus.Timeout.Destination";
        public const string TimeoutAt = "NServiceBus.Timeout.Expire";

        MsmqSettings settings;
        readonly string timeoutsQueue;
    }
}