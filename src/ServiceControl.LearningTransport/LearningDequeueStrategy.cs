namespace ServiceControl.LearningTransport
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Transports;
    using NServiceBus.Unicast.Transport;

    class LearningDequeueStrategy : IDequeueMessages, IDisposable
    {
        public LearningDequeueStrategy(Configure configure, CriticalError criticalError, LearningTransportUnitOfWork unitOfWork, PathCalculator pathCalculator)
        {
            this.configure = configure;
            this.criticalError = criticalError;
            this.unitOfWork = unitOfWork;
            this.pathCalculator = pathCalculator;
        }

        /// <summary>
        ///     Initializes the <see cref="IDequeueMessages" />.
        /// </summary>
        public void Init(Address address, TransactionSettings transactionSettings, Func<TransportMessage, bool> tryProcessMessage, Action<TransportMessage, Exception> endProcessMessage)
        {
            this.transactionSettings = transactionSettings;
            this.tryProcessMessage = tryProcessMessage;
            this.endProcessMessage = endProcessMessage;

            PathChecker.ThrowForBadPath(address.Queue, "InputQueue");

            endpointPaths = pathCalculator.PathsForEndpoint(address.Queue);

            delayedMessagePoller = new DelayedMessagePoller(endpointPaths);

            purgeOnStartup = configure.PurgeOnStartup();
        }

        /// <summary>
        ///     Starts the dequeuing of message using the specified <paramref name="maximumConcurrencyLevel" />.
        /// </summary>
        public void Start(int maximumConcurrencyLevel)
        {
            maxConcurrency = maximumConcurrencyLevel;
            concurrencyLimiter = new SemaphoreSlim(maxConcurrency);
            cancellationTokenSource = new CancellationTokenSource();

            cancellationToken = cancellationTokenSource.Token;

            if (purgeOnStartup)
            {
                if (Directory.Exists(endpointPaths.Header))
                {
                    Directory.Delete(endpointPaths.Header, true);
                }
            }

            RecoverPendingTransactions();

            EnsureDirectoriesExists();

            messagePumpTask = Task.Run(ProcessMessages, cancellationToken);

            delayedMessagePoller.Start();
        }

        /// <summary>
        ///     Stops the dequeuing of messages.
        /// </summary>
        public void Stop()
        {
            cancellationTokenSource.Cancel();

            delayedMessagePoller.Stop();

            messagePumpTask.Wait(CancellationToken.None);

            while (concurrencyLimiter.CurrentCount != maxConcurrency)
            {
                Thread.Sleep(50);
            }

            concurrencyLimiter.Dispose();
        }

        /// <summary>
        ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            // Injected
        }

        void RecoverPendingTransactions()
        {
            if (transactionSettings.IsTransactional)
            {
                DirectoryBasedTransaction.RecoverPartiallyCompletedTransactions(endpointPaths);
            }
            else
            {
                if (Directory.Exists(endpointPaths.Pending))
                {
                    Directory.Delete(endpointPaths.Pending, true);
                }
            }
        }

        void EnsureDirectoriesExists()
        {
            Directory.CreateDirectory(endpointPaths.Header);
            Directory.CreateDirectory(endpointPaths.Body);
            Directory.CreateDirectory(endpointPaths.Deferred);
            Directory.CreateDirectory(endpointPaths.Pending);

            if (transactionSettings.IsTransactional)
            {
                Directory.CreateDirectory(endpointPaths.Committed);
            }
        }

        [DebuggerNonUserCode]
        async Task ProcessMessages()
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await InnerProcessMessages()
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // graceful shutdown
                }
                catch (Exception ex)
                {
                    criticalError.Raise("Failure to process messages", ex);
                }
            }
        }

        async Task InnerProcessMessages()
        {
            log.Debug($"Started polling for new messages in {endpointPaths.Header}");

            while (!cancellationToken.IsCancellationRequested)
            {
                var filesFound = false;

                foreach (var filePath in Directory.EnumerateFiles(endpointPaths.Header, "*.*"))
                {
                    filesFound = true;

                    var nativeMessageId = Path.GetFileNameWithoutExtension(filePath).Replace(".metadata", "");

                    await concurrencyLimiter.WaitAsync(cancellationToken)
                        .ConfigureAwait(false);

                    ILearningTransportTransaction transaction;

                    try
                    {
                        transaction = GetTransaction();

                        var ableToLockFile = transaction.BeginTransaction(filePath);

                        if (!ableToLockFile)
                        {
                            log.Debug($"Unable to lock file {filePath}({transaction.FileToProcess})");
                            concurrencyLimiter.Release();
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Debug($"Failed to begin transaction {filePath}", ex);

                        concurrencyLimiter.Release();
                        throw;
                    }


                    ProcessFile(transaction, nativeMessageId);
                    try
                    {
                        if (log.IsDebugEnabled)
                        {
                            log.Debug($"Completing processing for {filePath}({transaction.FileToProcess})");
                        }

                        var wasCommitted = transaction.Complete();

                        if (wasCommitted)
                        {
                            FileOps.Delete(Path.Combine(endpointPaths.Body, nativeMessageId + PathCalculator.BodyFileSuffix));
                        }
                    }
                    catch (Exception ex)
                    {
                        criticalError.Raise($"Failure while trying to complete receive transaction for  {filePath}({transaction.FileToProcess})" + filePath, ex);
                    }
                    finally
                    {
                        concurrencyLimiter.Release();
                    }
                }

                if (!filesFound)
                {
                    await Task.Delay(10, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        ILearningTransportTransaction GetTransaction()
        {
            if (!transactionSettings.IsTransactional)
            {
                return new NoTransaction(endpointPaths.Header, endpointPaths.Pending);
            }

            return new DirectoryBasedTransaction(endpointPaths, Guid.NewGuid().ToString());
        }

        void ProcessFile(ILearningTransportTransaction transaction, string messageId)
        {
            var message = FileOps.ReadText(transaction.FileToProcess);

            var headers = HeaderSerializer.Deserialize(message);

            string ttbrString;
            if (headers.TryGetValue(LearningTransportHeaders.TimeToBeReceived, out ttbrString))
            {
                headers.Remove(LearningTransportHeaders.TimeToBeReceived);

                var ttbr = TimeSpan.Parse(ttbrString);

                //file.move preserves create time
                var sentTime = File.GetCreationTimeUtc(transaction.FileToProcess);

                var utcNow = DateTime.UtcNow;
                if (sentTime + ttbr < utcNow)
                {
                    transaction.Commit();
                    log.InfoFormat("Dropping message '{0}' as the specified TimeToBeReceived of '{1}' expired since sending the message at '{2:O}'. Current UTC time is '{3:O}'", messageId, ttbrString, sentTime, utcNow);
                    return;
                }
            }

            var tokenSource = new CancellationTokenSource();

            var bodyPath = Path.Combine(endpointPaths.Body, $"{messageId}{PathCalculator.BodyFileSuffix}");

            var body = FileOps.ReadBytes(bodyPath);

            var transportMessage = new TransportMessage(messageId, headers)
            {
                Body = body
            };

            Exception exception = null;

            unitOfWork.SetTransaction(transaction);

            try
            {
                if (tryProcessMessage(transportMessage))
                {
                    if (tokenSource.IsCancellationRequested)
                    {
                        transaction.Rollback();

                        return;
                    }

                    transaction.Commit();
                }
                else
                {
                    transaction.Rollback();
                }
            }
            catch (Exception ex)
            {
                exception = ex;
                transaction.Rollback();
            }
            finally
            {
                unitOfWork.ClearTransaction();
                endProcessMessage(transportMessage, exception);
            }
        }

        CancellationToken cancellationToken;
        CancellationTokenSource cancellationTokenSource;
        SemaphoreSlim concurrencyLimiter;
        Task messagePumpTask;

        Configure configure;
        readonly CriticalError criticalError;
        LearningTransportUnitOfWork unitOfWork;
        private readonly PathCalculator pathCalculator;

        TransactionSettings transactionSettings;
        Func<TransportMessage, bool> tryProcessMessage;
        Action<TransportMessage, Exception> endProcessMessage;

        bool purgeOnStartup;

        DelayedMessagePoller delayedMessagePoller;
        int maxConcurrency;

        static ILog log = LogManager.GetLogger<LearningDequeueStrategy>();
        private PathCalculator.EndpointBasePaths endpointPaths;
    }
}
