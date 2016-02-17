﻿namespace ServiceControl.ExternalIntegrations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.CircuitBreakers;
    using NServiceBus.Features;
    using NServiceBus.Logging;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;

    public class EventDispatcher : FeatureStartupTask
    {
        public IDocumentStore DocumentStore { get; set; }
        public IBus Bus { get; set; }
        public IEnumerable<IEventPublisher> EventPublishers { get; set; }
        public CriticalError CriticalError { get; set; }

        protected override void OnStart()
        {
            tokenSource = new CancellationTokenSource();
            circuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("EventDispatcher",
                TimeSpan.FromMinutes(2),
                ex => CriticalError.Raise("Repeated failures when dispatching external integration events.", ex),
                TimeSpan.FromSeconds(20));
            StartDispatcher();
        }

        void StartDispatcher()
        {
            Task.Factory
                .StartNew(() => DispatchEvents(tokenSource.Token), tokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default)
                .ContinueWith(t =>
                {
// ReSharper disable once PossibleNullReferenceException
                    t.Exception.Handle(ex =>
                    {
                        Logger.Error("An exception occurred when dispatching external integration events", ex);
                        circuitBreaker.Failure(ex);
                        return true;
                    });

                    if (!tokenSource.IsCancellationRequested)
                    {
                        StartDispatcher();
                    }
                }, TaskContinuationOptions.OnlyOnFaulted);
        }

        protected override void OnStop()
        {
            tokenSource.Cancel();
            resetEvent.Wait();
        }

        private void DispatchEvents(CancellationToken token)
        {
            try
            {
                resetEvent.Reset();
                while (!token.IsCancellationRequested)
                {
                    if (DispatchEventBatch() && !token.IsCancellationRequested)
                    {
                        token.WaitHandle.WaitOne(TimeSpan.FromSeconds(1));
                    }
                }
            }
            finally
            {
                resetEvent.Set();
            }
        }

        bool DispatchEventBatch()
        {
            using (var session = DocumentStore.OpenSession())
            {
                var awaitingDispatching = session.Query<ExternalIntegrationDispatchRequest>().Take(Settings.ExternalIntegrationsDispatchingBatchSize).ToList();
                if (!awaitingDispatching.Any())
                {
                    if (Logger.IsDebugEnabled)
                    {
                        Logger.Debug("Nothing to dispatch. Waiting...");
                    }
                    return true;
                }

                var allContexts = awaitingDispatching.Select(r => r.DispatchContext).ToArray();
                if (Logger.IsDebugEnabled)
                {
                    Logger.DebugFormat("Dispatching {0} events.", allContexts.Length);
                }
                var eventsToBePublished = EventPublishers.SelectMany(p => p.PublishEventsForOwnContexts(allContexts, session));

                foreach (var eventToBePublished in eventsToBePublished)
                {
                    if (Logger.IsDebugEnabled)
                    {
                        Logger.DebugFormat("Publishing external event on the bus.");
                    }

                    try
                    {
                        Bus.Publish(eventToBePublished);
                    }
                    catch (Exception e)
                    {
                        Logger.Error("Failed dispatching external integration event.", e);

                        var publishedEvent = eventToBePublished;
                        Bus.Publish<ExternalIntegrationEventFailedToBePublished>(m =>
                        {
                            m.EventType = publishedEvent.GetType();
                            try
                            {
                                m.Reason = e.GetBaseException().Message;
                            }
                            catch (Exception)
                            {
                                m.Reason = "Failed to retrieve reason!";
                            }
                        });
                    }
                }
                foreach (var dispatchedEvent in awaitingDispatching)
                {
                    session.Delete(dispatchedEvent);
                }
                session.SaveChanges();
            }

            return false;
        }

        ManualResetEventSlim resetEvent = new ManualResetEventSlim(false);
        CancellationTokenSource tokenSource;
        RepeatedFailuresOverTimeCircuitBreaker circuitBreaker;
        static readonly ILog Logger = LogManager.GetLogger(typeof(EventDispatcher));
    }
}