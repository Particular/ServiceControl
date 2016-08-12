namespace NServiceBus.AcceptanceTesting.Support
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;
    using NServiceBus.Configuration.AdvanceExtensibility;
    using NServiceBus.Support;
    using NServiceBus.Unicast;
    using Transports;

    public class EndpointRunner
    {
        static ILog Logger = LogManager.GetLogger<EndpointRunner>();
        EndpointBehavior behavior;
        IStartableBus bus;
        ISendOnlyBus sendOnlyBus;
        EndpointConfiguration configuration;
        Task executeWhens;
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

                if (!string.IsNullOrEmpty(configuration.CustomMachineName))
                {
                    RuntimeEnvironment.MachineNameAction = () => configuration.CustomMachineName;
                }

                //apply custom config settings
                busConfiguration = configuration.GetConfiguration(run, routingTable);

                endpointBehavior.CustomConfig.ForEach(customAction => customAction(busConfiguration));

                if (configuration.SendOnly)
                {
                    sendOnlyBus = Bus.CreateSendOnly(busConfiguration);
                }
                else 
                {
                    bus = configuration.GetBus() ?? Bus.Create(busConfiguration);
                    var transportDefinition = ((UnicastBus)bus).Settings.Get<TransportDefinition>();

                    scenarioContext.HasNativePubSubSupport = transportDefinition.HasNativePubSubSupport;
                }

                return Result.Success();
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to initialize endpoint {endpointName}", ex);
                return Result.Failure(ex);
            }
        }

        public Task ExecuteWhens()
        {
            stopToken = stopSource.Token;

            if (behavior.Whens.Count == 0)
            {
                executeWhens = Task.FromResult(0);
            }
            else
            {
                executeWhens = Task.Run(() =>
                {
                    var executedWhens = new List<Guid>();

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
                                return;
                            }

                            if (executedWhens.Contains(when.Id))
                            {
                                continue;
                            }

                            if (when.ExecuteAction(scenarioContext, bus))
                            {
                                executedWhens.Add(when.Id);
                            }
                        }
                    }
                });
            }

            return executeWhens;
        }

        public Result Start()
        {
            try
            {
                foreach (var given in behavior.Givens)
                {
                    var action = given.GetAction(scenarioContext);

                    if (configuration.SendOnly)
                    {
                        action(new IBusAdapter(sendOnlyBus));
                    }
                    else
                    {

                        action(bus);
                    }
                }

                if (!configuration.SendOnly)
                {
                    bus.Start();
                }

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

                try
                {
                    executeWhens.Wait();
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to execute whens", ex);
                }

                if (configuration.SendOnly)
                {
                    sendOnlyBus.Dispose();
                }
                else
                {
                    if (configuration.StopBus != null)
                    {
                        configuration.StopBus();
                    }
                    else
                    {
                        bus.Dispose();
                    }
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