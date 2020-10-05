namespace ServiceControl.ExternalIntegrations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Logging;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Commands.Batches;
    using Raven.Client.Documents.Session;
    using Raven.Client.Documents.Subscriptions;
    using Raven.Client.Exceptions.Database;
    using Raven.Client.Exceptions.Documents.Subscriptions;
    using Raven.Client.Exceptions.Security;

    class EventDispatcher : FeatureStartupTask, IEventDispatcher
    {
        public EventDispatcher(IDocumentStore store, IDomainEvents domainEvents, CriticalError criticalError, IEnumerable<IEventPublisher> eventPublishers)
        {
            this.store = store;
            this.eventPublishers = eventPublishers;
            this.domainEvents = domainEvents;
        }

        protected override Task OnStart(IMessageSession session)
        {
            tokenSource = new CancellationTokenSource();
            bus = session;

            task = StartDispatcherTask();
            return Task.FromResult(0);
        }

        public async Task Enqueue(object[] dispatchContexts)
        {
            var bulkInsert = store.BulkInsert();

            foreach (var dispatchContext in dispatchContexts)
            {
                await bulkInsert.StoreAsync(new ExternalIntegrationDispatchRequest
                {
                    DispatchContext = dispatchContext
                }, $"ExternalIntegrationDispatchRequests/{Guid.NewGuid()}").ConfigureAwait(false);
            }

            await bulkInsert.DisposeAsync().ConfigureAwait(false);
        }

        async Task StartDispatcherTask()
        {
            var name = await store.Subscriptions.CreateAsync(new SubscriptionCreationOptions<ExternalIntegrationDispatchRequest>
            {
                Name = "ExternalIntegrationEvents"
            }).ConfigureAwait(false);

            while (true)
            {
                var options = new SubscriptionWorkerOptions(name)
                {
                    //Values copied from RavenDB docs
                    MaxErroneousPeriod = TimeSpan.FromHours(2),
                    TimeToWaitBeforeConnectionRetry = TimeSpan.FromMinutes(2),
                    Strategy = SubscriptionOpeningStrategy.TakeOver
                };

                subscriptionWorker = store.Subscriptions.GetSubscriptionWorker<ExternalIntegrationDispatchRequest>(options);

                try
                {
                    // here we are able to be informed of any exception that happens during processing                    
                    subscriptionWorker.OnSubscriptionConnectionRetry += exception =>
                    {
                        Logger.Error("Retrying connection for ExternalIntegrationEvents subscription.", exception);
                    };

                    await subscriptionWorker.Run(async batch =>
                    {
                        using (var session = batch.OpenAsyncSession())
                        {
                            await Dispatch(batch.Items.Select(x => x.Result), session).ConfigureAwait(false);
                            
                            IList<ICommandData> deleteCommands = batch.Items.Select(x => new DeleteCommandData(x.Id, x.ChangeVector)).ToArray();
                            var batchCommand = new SingleNodeBatchCommand(store.Conventions, session.Advanced.Context, deleteCommands);
                            await session.Advanced.RequestExecutor.ExecuteAsync(batchCommand, session.Advanced.Context).ConfigureAwait(false);
                        }
                    }, tokenSource.Token).ConfigureAwait(false);

                    // Run will complete normally if you have disposed the subscription
                    return;
                }
                catch (Exception e)
                {
                    Logger.Error("Failure in subscription ExternalIntegrationEvents", e);

                    if (e is DatabaseDoesNotExistException ||
                        e is SubscriptionDoesNotExistException ||
                        e is SubscriptionInvalidStateException ||
                        e is AuthorizationException)
                    {
                        throw; // not recoverable
                    }

                    if (e is SubscriptionClosedException)
                        // closed explicitly by admin, probably
                    {
                        return;
                    }

                    if (e is SubscriberErrorException)
                    {
                        Logger.Error("Failed dispatching external integration event.", e);
                        continue;
                    }
                    return;
                }
                finally
                {
                    await subscriptionWorker.DisposeAsync().ConfigureAwait(false);
                    subscriptionWorker = null;
                }
            }
        }

        async Task Dispatch(IEnumerable<ExternalIntegrationDispatchRequest> awaitingDispatching, IAsyncDocumentSession session)
        {
            var allContexts = awaitingDispatching.Select(r => r.DispatchContext).ToArray();
            if (Logger.IsDebugEnabled)
            {
                Logger.Debug($"Dispatching {allContexts.Length} events.");
            }

            var eventsToBePublished = new List<object>();
            foreach (var publisher in eventPublishers)
            {
                var events = await publisher.PublishEventsForOwnContexts(allContexts, session)
                    .ConfigureAwait(false);
                eventsToBePublished.AddRange(events);
            }

            foreach (var eventToBePublished in eventsToBePublished)
            {
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug("Publishing external event on the bus.");
                }

                try
                {
                    await bus.Publish(eventToBePublished)
                        .ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Logger.Error("Failed dispatching external integration event.", e);

                    var m = new ExternalIntegrationEventFailedToBePublished
                    {
                        EventType = eventToBePublished.GetType()
                    };
                    try
                    {
                        m.Reason = e.GetBaseException().Message;
                    }
                    catch (Exception)
                    {
                        m.Reason = "Failed to retrieve reason!";
                    }

                    await domainEvents.Raise(m)
                        .ConfigureAwait(false);
                }
            }
        }

        protected override async Task OnStop(IMessageSession session)
        {
            tokenSource.Cancel();

            if (task != null)
            {
                if (subscriptionWorker != null)
                {
                    await subscriptionWorker.DisposeAsync().ConfigureAwait(false);
                }
                await task.ConfigureAwait(false);
            }

            tokenSource.Dispose();
        }

        IMessageSession bus;
        IEnumerable<IEventPublisher> eventPublishers;
        IDocumentStore store;
        IDomainEvents domainEvents;
        Task task;
        CancellationTokenSource tokenSource;
        static ILog Logger = LogManager.GetLogger(typeof(EventDispatcher));
        SubscriptionWorker<ExternalIntegrationDispatchRequest> subscriptionWorker;
    }
}