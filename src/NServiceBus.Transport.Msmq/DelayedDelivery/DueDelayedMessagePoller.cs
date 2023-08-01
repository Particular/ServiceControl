namespace NServiceBus.Transport.Msmq.DelayedDelivery
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using System.Transactions;
    using Logging;
    using Extensibility;
    using Routing;
    using Unicast.Queuing;

    class DueDelayedMessagePoller
    {
        public DueDelayedMessagePoller(
            MsmqMessageDispatcher dispatcher,
            IDelayedMessageStore delayedMessageStore,
            int numberOfRetries,
            string timeoutsErrorQueue,
            Dictionary<string, string> faultMetadata,
            TimeSpan timeToTriggerFetchCircuitBreaker,
            TimeSpan timeToTriggerDispatchCircuitBreaker,
            int maximumRecoveryFailuresPerSecond
        )
        {
            this.delayedMessageStore = delayedMessageStore;
            errorQueue = timeoutsErrorQueue;
            this.faultMetadata = faultMetadata;
            this.timeToTriggerFetchCircuitBreaker = timeToTriggerFetchCircuitBreaker;
            this.timeToTriggerDispatchCircuitBreaker = timeToTriggerDispatchCircuitBreaker;
            this.maximumRecoveryFailuresPerSecond = maximumRecoveryFailuresPerSecond;
            this.numberOfRetries = numberOfRetries;
            this.dispatcher = dispatcher;

            signalQueue = Channel.CreateBounded<bool>(1);
            taskQueue = Channel.CreateBounded<Task>(2);
        }

        public void Init(CriticalError criticalError, PushSettings pushSettings)
        {
            txOption = pushSettings.RequiredTransactionMode == TransportTransactionMode.TransactionScope
                ? TransactionScopeOption.Required
                : TransactionScopeOption.RequiresNew;

            fetchCircuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("MsmqDelayedMessageFetch", timeToTriggerFetchCircuitBreaker,
                ex => criticalError.Raise("Failed to fetch due delayed messages from the storage", ex));

            dispatchCircuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("MsmqDelayedMessageDispatch", timeToTriggerDispatchCircuitBreaker,
                ex => criticalError.Raise("Failed to dispatch delayed messages to destination", ex));

            failureHandlingCircuitBreaker = new FailureRateCircuitBreaker("MsmqDelayedMessageFailureHandling", maximumRecoveryFailuresPerSecond,
                ex => criticalError.Raise("Failed to execute error handling for delayed message forwarding", ex));
        }

        public void Start()
        {
            tokenSource = new CancellationTokenSource();
            loopTask = Task.Run(() => Loop(tokenSource.Token));
            completionTask = Task.Run(() => AwaitHandleTasks());
        }

        public async Task Stop()
        {
            tokenSource.Cancel();
            await loopTask.ConfigureAwait(false);
            await completionTask.ConfigureAwait(false);
        }

        public void Signal(DateTimeOffset timeoutTime)
        {
            //If the next timeout is within a minute from now, trigger the poller
            if (DateTimeOffset.UtcNow.Add(MaxSleepDuration) > timeoutTime)
            {
                //If there is something already in the queue we are fine.
                signalQueue.Writer.TryWrite(true);
            }
        }

        /// <summary>
        /// This method does not accept a cancellation token because it needs to finish all the tasks that have been started even if cancellation is under way.
        /// </summary>
        async Task AwaitHandleTasks()
        {
            while (await taskQueue.Reader.WaitToReadAsync().ConfigureAwait(false)) //if this returns false the channel is completed
            {
                while (taskQueue.Reader.TryRead(out var task))
                {
                    try
                    {
                        await task.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        Log.Debug("A shutdown was triggered and Poll task has been cancelled.");
                    }
                    catch (Exception e)
                    {
                        failureHandlingCircuitBreaker.Failure(e);
                    }
                }
            }
        }

        async Task Loop(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {

                        //We create a TaskCompletionSource source here and pass it to spawned Poll task. Once the Poll task is done with fetching
                        //the next due timeout, we continue with the loop. This allows us to run the Poll tasks in an overlapping way so that at any time there is
                        //one Poll task fetching and one dispatching.

                        var completionSource = new TaskCompletionSource<DateTimeOffset?>();
                        var handleTask = Poll(completionSource, cancellationToken);
                        await taskQueue.Writer.WriteAsync(handleTask, cancellationToken).ConfigureAwait(false);
                        var nextPoll = await completionSource.Task.ConfigureAwait(false);

                        if (nextPoll.HasValue)
                        {
                            //We wait either for a signal that a new delayed message has been stored or until next timeout should be due
                            //After waiting we cancel the token so that the task waiting for the signal is cancelled and does not "eat" the next
                            //signal when it is raised in the next iteration of this loop
                            using (var waitCancelled = new CancellationTokenSource())
                            {
                                var combinedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, waitCancelled.Token);

                                var waitTask = WaitIfNeeded(nextPoll.Value, combinedSource.Token);
                                var signalTask = WaitForSignal(combinedSource.Token);

                                await Task.WhenAny(waitTask, signalTask).ConfigureAwait(false);
                                waitCancelled.Cancel();
                            }
                        }
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        Log.Debug("A shutdown was triggered, canceling the Loop operation.");
                        break;
                    }
                    catch (Exception e)
                    {
                        Log.Error("Failed to poll and dispatch due timeouts from storage.", e);
                        //Poll and HandleDueDelayedMessage have their own exception handling logic so any exception here is likely going to be related to the transaction itself
                        await fetchCircuitBreaker.Failure(e).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                //No matter what we need to complete the writer so that we can stop gracefully
                taskQueue.Writer.Complete();
            }
        }

        async Task WaitForSignal(CancellationToken cancellationToken)
        {
            try
            {
                await signalQueue.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Log.Debug("No new delayed messages have been stored while waiting for the next due delayed message.");
            }
        }

        Task WaitIfNeeded(DateTimeOffset nextPoll, CancellationToken cancellationToken)
        {
            var waitTime = nextPoll - DateTimeOffset.UtcNow;
            if (waitTime > TimeSpan.Zero)
            {
                return Task.Delay(waitTime, cancellationToken);
            }

            return Task.CompletedTask;
        }

        async Task Poll(TaskCompletionSource<DateTimeOffset?> result, CancellationToken cancellationToken)
        {
            DelayedMessage timeout = null;
            DateTimeOffset now = DateTimeOffset.UtcNow;
            try
            {
                using (var tx = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled))
                {
                    timeout = await delayedMessageStore.FetchNextDueTimeout(now, cancellationToken).ConfigureAwait(false);
                    fetchCircuitBreaker.Success();

                    if (timeout != null)
                    {
                        result.SetResult(null);
                        HandleDueDelayedMessage(timeout);

                        var success = await delayedMessageStore.Remove(timeout, cancellationToken).ConfigureAwait(false);
                        if (!success)
                        {
                            Log.WarnFormat("Potential more-than-once dispatch as delayed message already removed from storage.");
                        }
                    }
                    else
                    {
                        using (new TransactionScope(TransactionScopeOption.RequiresNew, transactionOptions, TransactionScopeAsyncFlowOption.Enabled))
                        {
                            var nextDueTimeout = await delayedMessageStore.Next(cancellationToken).ConfigureAwait(false);
                            if (nextDueTimeout.HasValue)
                            {
                                result.SetResult(nextDueTimeout);
                            }
                            else
                            {
                                //If no timeouts, wait a while
                                result.SetResult(now.Add(MaxSleepDuration));
                            }
                        }
                    }
                    tx.Complete();
                }
            }
            catch (QueueNotFoundException exception)
            {
                if (timeout != null)
                {
                    await TrySendDelayedMessageToErrorQueue(timeout, exception, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                //Shutting down. Bubble up the exception. Will be awaited by AwaitHandleTasks method that catches OperationCanceledExceptions
                throw;
            }
            catch (Exception exception)
            {
                Log.Error("Failure during timeout polling.", exception);
                if (timeout != null)
                {
                    await dispatchCircuitBreaker.Failure(exception).ConfigureAwait(false);
                    using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, transactionOptions, TransactionScopeAsyncFlowOption.Enabled))
                    {
                        await delayedMessageStore.IncrementFailureCount(timeout, cancellationToken).ConfigureAwait(false);
                        scope.Complete();
                    }

                    if (timeout.NumberOfRetries > numberOfRetries)
                    {
                        await TrySendDelayedMessageToErrorQueue(timeout, exception, cancellationToken).ConfigureAwait(false);
                    }
                }
                else
                {
                    await fetchCircuitBreaker.Failure(exception).ConfigureAwait(false);
                }
            }
            finally
            {
                //In case we failed before SetResult
                result.TrySetResult(null);
            }
        }

        void HandleDueDelayedMessage(DelayedMessage timeout)
        {
            TimeSpan diff = DateTimeOffset.UtcNow - new DateTimeOffset(timeout.Time, TimeSpan.Zero);

            Log.DebugFormat("Timeout {0} over due for {1}", timeout.MessageId, diff);

            using (var tx = new TransactionScope(txOption, transactionOptions, TransactionScopeAsyncFlowOption.Enabled))
            {
                var transportTransaction = new TransportTransaction();
                transportTransaction.Set(Transaction.Current);
                dispatcher.DispatchDelayedMessage(
                    timeout.MessageId,
                    timeout.Headers,
                    timeout.Body,
                    timeout.Destination,
                    transportTransaction,
                    new ContextBag()
                );

                tx.Complete();
            }

            dispatchCircuitBreaker.Success();
        }

        async Task TrySendDelayedMessageToErrorQueue(DelayedMessage timeout, Exception exception, CancellationToken cancellationToken)
        {
            try
            {
                bool success = await delayedMessageStore.Remove(timeout, cancellationToken).ConfigureAwait(false);

                if (!success)
                {
                    // Already dispatched
                    return;
                }

                Dictionary<string, string> headersAndProperties = MsmqUtilities.DeserializeMessageHeaders(timeout.Headers);

                ExceptionHeaderHelper.SetExceptionHeaders(headersAndProperties, exception);

                foreach (KeyValuePair<string, string> pair in faultMetadata)
                {
                    headersAndProperties[pair.Key] = pair.Value;
                }

                Log.InfoFormat("Move {0} to error queue", timeout.MessageId);
                using (var transportTx = new TransactionScope(txOption, transactionOptions, TransactionScopeAsyncFlowOption.Enabled))
                {
                    var transportTransaction = new TransportTransaction();
                    transportTransaction.Set(Transaction.Current);

                    var outgoingMessage = new OutgoingMessage(timeout.MessageId, headersAndProperties, timeout.Body);
                    var transportOperation = new TransportOperation(outgoingMessage, new UnicastAddressTag(errorQueue));
                    await dispatcher.Dispatch(new TransportOperations(transportOperation), transportTransaction, new ContextBag())
                        .ConfigureAwait(false);

                    transportTx.Complete();
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                //Shutting down
                Log.Debug("Aborted sending delayed message to error queue due to shutdown.");
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to move delayed message {timeout.MessageId} to the error queue {errorQueue} after {timeout.NumberOfRetries} failed attempts at dispatching it to the destination", ex);
            }
        }

        static readonly ILog Log = LogManager.GetLogger<DueDelayedMessagePoller>();
        static readonly TimeSpan MaxSleepDuration = TimeSpan.FromMinutes(1);

        readonly Dictionary<string, string> faultMetadata;
        readonly TimeSpan timeToTriggerFetchCircuitBreaker;
        readonly TimeSpan timeToTriggerDispatchCircuitBreaker;
        readonly int maximumRecoveryFailuresPerSecond;

        IDelayedMessageStore delayedMessageStore;
        MsmqMessageDispatcher dispatcher;
        string errorQueue;
        int numberOfRetries;
        RepeatedFailuresOverTimeCircuitBreaker fetchCircuitBreaker;
        RepeatedFailuresOverTimeCircuitBreaker dispatchCircuitBreaker;
        FailureRateCircuitBreaker failureHandlingCircuitBreaker;
        Task loopTask;
        Task completionTask;
        Channel<bool> signalQueue;
        Channel<Task> taskQueue;
        CancellationTokenSource tokenSource;
        TransactionScopeOption txOption;
        TransactionOptions transactionOptions = new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted };
    }
}
