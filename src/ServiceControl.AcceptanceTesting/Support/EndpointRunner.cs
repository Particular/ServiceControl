﻿namespace NServiceBus.AcceptanceTesting.Support
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;
    using NServiceBus.Configuration.AdvanceExtensibility;
    using NServiceBus.Unicast;
    using Transports;

    public class EndpointRunner
    {
        static ILog Logger = LogManager.GetLogger<EndpointRunner>();
        EndpointBehavior behavior;
        IStartableBus bus;
        EndpointConfiguration configuration;
        ScenarioContext scenarioContext;
        BusConfiguration busConfiguration;
        CancellationToken stopToken;
        readonly CancellationTokenSource stopSource = new CancellationTokenSource();

        public Result Initialize(RunDescriptor run, EndpointBehavior endpointBehavior,
            IDictionary<Type, string> routingTable, string endpointName)
        {
            try
            {
                behavior = endpointBehavior;
                scenarioContext = run.ScenarioContext;
                configuration =
                    ((IEndpointConfigurationFactory)Activator.CreateInstance(endpointBehavior.EndpointBuilderType))
                        .Get();
                configuration.EndpointName = endpointName;

                //apply custom config settings
                busConfiguration = configuration.GetConfiguration(run, routingTable);

                endpointBehavior.CustomConfig.ForEach(customAction => customAction(busConfiguration));

                bus = configuration.GetBus() ?? Bus.Create(busConfiguration);
                var transportDefinition = ((UnicastBus)bus).Settings.Get<TransportDefinition>();

                scenarioContext.HasNativePubSubSupport = transportDefinition.HasNativePubSubSupport;

                return Result.Success();
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to initialize endpoint {endpointName}", ex);
                return Result.Failure(ex);
            }
        }

        public async Task ExecuteWhens()
        {
            stopToken = stopSource.Token;

            try
            {
                if (behavior.Whens.Count != 0)
                {
                    await Task.Run(async () =>
                    {
                        var executedWhens = new HashSet<Guid>();

                        while (!stopToken.IsCancellationRequested)
                        {
                            if (executedWhens.Count == behavior.Whens.Count)
                            {
                                break;
                            }

                            if (stopToken.IsCancellationRequested)
                            {
                                break;
                            }

                            foreach (var when in behavior.Whens)
                            {
                                if (stopToken.IsCancellationRequested)
                                {
                                    break;
                                }

                                if (executedWhens.Contains(when.Id))
                                {
                                    continue;
                                }

                                if (await when.ExecuteAction(scenarioContext, bus).ConfigureAwait(false))
                                {
                                    executedWhens.Add(when.Id);
                                }
                                else
                                {
                                    await Task.Delay(100, CancellationToken.None).ConfigureAwait(false);
                                }
                            }

                            await Task.Yield(); // enforce yield current context, tight loop could introduce starvation
                        }
                    }, stopToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to execute Whens on endpoint{configuration.EndpointName}", ex);

                throw;
            }
        }

        public Result Start()
        {
            try
            {
                foreach (var given in behavior.Givens)
                {
                    var action = given.GetAction(scenarioContext);

                    action(bus);
                }

                bus.Start();

                return Result.Success();
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to start endpoint {configuration.EndpointName}", ex);

                return Result.Failure(ex);
            }
        }

        public Result Stop()
        {
            try
            {
                stopSource.Cancel();

                if (configuration.StopBus != null)
                {
                    configuration.StopBus();
                }
                else
                {
                    bus.Dispose();
                }

                Cleanup();

                return Result.Success();
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to stop endpoint {configuration.EndpointName}", ex);

                return Result.Failure(ex);
            }
        }

        void Cleanup()
        {
            Action transportCleaner;

            if (busConfiguration.GetSettings().TryGet("CleanupTransport", out transportCleaner))
            {
                transportCleaner();
            }
        }

        public string Name()
        {
            return configuration.EndpointName;
        }

        public class Result
        {
            public Exception Exception { get; set; }

            public bool Failed => Exception != null;

            public static Result Success()
            {
                return new Result();
            }

            public static Result Failure(Exception ex)
            {
                var baseException = ex.GetBaseException();

                if (ex.GetType().IsSerializable)
                {
                    return new Result
                    {
                        Exception = baseException
                    };
                }

                return new Result
                {
                    Exception = new Exception(baseException.Message)
                };
            }
        }
    }
}