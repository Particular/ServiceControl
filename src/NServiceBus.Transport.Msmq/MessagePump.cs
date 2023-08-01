namespace NServiceBus.Transport.Msmq
{
    using System;
    using System.Diagnostics;
    using MSMQ.Messaging;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;
    using Support;
    using Transport;

    class MessagePump : IPushMessages, IDisposable
    {
        public MessagePump(Func<TransportTransactionMode, ReceiveStrategy> receiveStrategyFactory, TimeSpan messageEnumeratorTimeout, bool discardExpiredTtbrMessages)
        {
            this.receiveStrategyFactory = receiveStrategyFactory;
            this.messageEnumeratorTimeout = messageEnumeratorTimeout;
            this.discardExpiredTtbrMessages = discardExpiredTtbrMessages;
        }

        public void Dispose()
        {
            // Injected
        }

        public Task Init(Func<MessageContext, Task> onMessage, Func<ErrorContext, Task<ErrorHandleResult>> onError, CriticalError criticalError, PushSettings settings)
        {
            peekCircuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("MsmqPeek", TimeSpan.FromSeconds(30), ex => criticalError.Raise("Failed to peek " + settings.InputQueue, ex));
            receiveCircuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("MsmqReceive", TimeSpan.FromSeconds(30), ex => criticalError.Raise("Failed to receive from " + settings.InputQueue, ex));

            var inputAddress = MsmqAddress.Parse(settings.InputQueue);
            var errorAddress = MsmqAddress.Parse(settings.ErrorQueue);

            if (!string.Equals(inputAddress.Machine, RuntimeEnvironment.MachineName, StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception($"MSMQ Dequeuing can only run against the local machine. Invalid inputQueue name '{settings.InputQueue}'.");
            }

            inputQueue = new MessageQueue(inputAddress.FullPath, false, true, QueueAccessMode.Receive);
            errorQueue = new MessageQueue(errorAddress.FullPath, false, true, QueueAccessMode.Send);

            if (settings.RequiredTransactionMode != TransportTransactionMode.None && !QueueIsTransactional())
            {
                throw new ArgumentException($"Queue must be transactional if you configure the endpoint to be transactional ({settings.InputQueue}).");
            }

            inputQueue.MessageReadPropertyFilter = DefaultReadPropertyFilter;

            if (settings.PurgeOnStartup)
            {
                inputQueue.Purge();
            }

            receiveStrategy = receiveStrategyFactory(settings.RequiredTransactionMode);

            receiveStrategy.Init(inputQueue, errorQueue, onMessage, onError, criticalError, discardExpiredTtbrMessages);

            return TaskEx.CompletedTask;
        }

        public void Start(PushRuntimeSettings limitations)
        {
            MessageQueue.ClearConnectionCache();

            maxConcurrency = limitations.MaxConcurrency;
            concurrencyLimiter = new SemaphoreSlim(limitations.MaxConcurrency, limitations.MaxConcurrency);
            cancellationTokenSource = new CancellationTokenSource();

            cancellationToken = cancellationTokenSource.Token;
            // ReSharper disable once ConvertClosureToMethodGroup
            // LongRunning is useless combined with async/await
            messagePumpTask = Task.Run(() => ProcessMessages(), CancellationToken.None);
        }

        public async Task Stop()
        {
            cancellationTokenSource.Cancel();

            await messagePumpTask.ConfigureAwait(false);

            try
            {
                using (var shutdownCancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                {
                    while (concurrencyLimiter.CurrentCount != maxConcurrency)
                    {
                        await Task.Delay(50, shutdownCancellationTokenSource.Token).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Error("The message pump failed to stop with in the time allowed(30s)");
            }

            concurrencyLimiter.Dispose();
            inputQueue.Dispose();
            errorQueue.Dispose();
        }

        [DebuggerNonUserCode]
        async Task ProcessMessages()
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await InnerProcessMessages().ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // For graceful shutdown purposes
                }
                catch (Exception ex)
                {
                    Logger.Error("MSMQ Message pump failed", ex);
                    await peekCircuitBreaker.Failure(ex).ConfigureAwait(false);
                }
            }
        }

        async Task InnerProcessMessages()
        {
            using (var enumerator = inputQueue.GetMessageEnumerator2())
            {
                while (!cancellationTokenSource.IsCancellationRequested)
                {
                    try
                    {
                        //note: .Peek will throw an ex if no message is available. It also turns out that .MoveNext is faster since message isn't read
                        if (!enumerator.MoveNext(messageEnumeratorTimeout))
                        {
                            continue;
                        }

                        peekCircuitBreaker.Success();
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("MSMQ receive operation failed", ex);
                        await peekCircuitBreaker.Failure(ex).ConfigureAwait(false);
                        continue;
                    }

                    if (cancellationTokenSource.IsCancellationRequested)
                    {
                        return;
                    }

                    await concurrencyLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);

                    _ = ReceiveMessage();
                }
            }
        }

        Task ReceiveMessage()
        {
            return TaskEx.Run(async state =>
            {
                var messagePump = (MessagePump)state;

                try
                {
                    await messagePump.receiveStrategy.ReceiveMessage().ConfigureAwait(false);
                    messagePump.receiveCircuitBreaker.Success();
                }
                catch (OperationCanceledException)
                {
                    // Intentionally ignored
                }
                catch (Exception ex)
                {
                    Logger.Warn("MSMQ receive operation failed", ex);
                    await messagePump.receiveCircuitBreaker.Failure(ex).ConfigureAwait(false);
                }
                finally
                {
                    messagePump.concurrencyLimiter.Release();
                }
            }, this);
        }

        bool QueueIsTransactional()
        {
            try
            {
                return inputQueue.Transactional;
            }
            catch (MessageQueueException msmqEx)
            {
                var error = $"There is a problem with the input inputQueue: {inputQueue.Path}. See the enclosed exception for details.";
                if (msmqEx.MessageQueueErrorCode == MessageQueueErrorCode.QueueNotFound)
                {
                    error = $"The queue {inputQueue.Path} does not exist. Run the CreateQueues.ps1 script included in the project output, or enable queue creation on startup using EndpointConfiguration.EnableInstallers().";
                }
                if (msmqEx.MessageQueueErrorCode == MessageQueueErrorCode.AccessDenied)
                {
                    error = $"Access denied for the queue {inputQueue.Path}. Ensure the user has Get Properties permission on the queue.";
                }
                throw new Exception(error, msmqEx);
            }
            catch (Exception ex)
            {
                var error = $"There is a problem with the input inputQueue: {inputQueue.Path}. See the enclosed exception for details.";
                throw new Exception(error, ex);
            }
        }

        CancellationToken cancellationToken;
        CancellationTokenSource cancellationTokenSource;
        int maxConcurrency;
        SemaphoreSlim concurrencyLimiter;
        MessageQueue errorQueue;
        MessageQueue inputQueue;

        Task messagePumpTask;

        ReceiveStrategy receiveStrategy;

        RepeatedFailuresOverTimeCircuitBreaker peekCircuitBreaker;
        RepeatedFailuresOverTimeCircuitBreaker receiveCircuitBreaker;
        Func<TransportTransactionMode, ReceiveStrategy> receiveStrategyFactory;
        TimeSpan messageEnumeratorTimeout;
        bool discardExpiredTtbrMessages;

        static ILog Logger = LogManager.GetLogger<MessagePump>();

        static MessagePropertyFilter DefaultReadPropertyFilter = new MessagePropertyFilter
        {
            Body = true,
            TimeToBeReceived = true,
            Recoverable = true,
            Id = true,
            ResponseQueue = true,
            CorrelationId = true,
            Extension = true,
            AppSpecific = true,
            UseJournalQueue = true,
            UseDeadLetterQueue = true
        };
    }
}