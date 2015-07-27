namespace NServiceBus.AcceptanceTesting.Support
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Remoting.Lifetime;
    using System.Threading;
    using System.Threading.Tasks;
    using Installation.Environments;
    using Logging;
    using NServiceBus.Settings;
    using NServiceBus.Support;

    [Serializable]
    public class EndpointRunner : MarshalByRefObject
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof (EndpointRunner));
        private readonly SemaphoreSlim contextChanged = new SemaphoreSlim(0);
        private EndpointBehavior behavior;
        private IStartableBus bus;
        private Configure config;
        private EndpointConfiguration configuration;
        private Task executeWhens;
        private ScenarioContext scenarioContext;
        readonly CancellationTokenSource stopSource = new CancellationTokenSource();
        CancellationToken stopToken;

        public Result Initialize(RunDescriptor run, EndpointBehavior endpointBehavior,
            IDictionary<Type, string> routingTable, string endpointName)
        {
            try
            {
                behavior = endpointBehavior;
                scenarioContext = run.ScenarioContext;
                configuration =
                    ((IEndpointConfigurationFactory) Activator.CreateInstance(endpointBehavior.EndpointBuilderType))
                        .Get();
                configuration.EndpointName = endpointName;

                if (!string.IsNullOrEmpty(configuration.CustomMachineName))
                {
                    RuntimeEnvironment.MachineNameAction = () => configuration.CustomMachineName;
                }

                //apply custom config settings
                endpointBehavior.CustomConfig.ForEach(customAction => customAction(config));
                config = configuration.GetConfiguration(run, routingTable);

                if (scenarioContext != null)
                {
                    config.Configurer.RegisterSingleton(scenarioContext.GetType(), scenarioContext);
                    scenarioContext.ContextPropertyChanged += scenarioContext_ContextPropertyChanged;
                }

                bus = config.CreateBus();

                Configure.Instance.ForInstallationOn<Windows>().Install();

                stopToken = stopSource.Token;

                if (behavior.Whens.Count == 0)
                {
                    executeWhens = Task.FromResult(0);
                }
                else
                {
                    executeWhens = Task.Factory.StartNew(async () =>
                    {
                        var executedWhens = new List<Guid>();

                        while (!stopToken.IsCancellationRequested)
                        {
                            if (executedWhens.Count == behavior.Whens.Count)
                            {
                                break;
                            }

                            //we spin around each 5s since the callback mechanism seems to be shaky
                            await contextChanged.WaitAsync(TimeSpan.FromSeconds(5), stopToken);

                            if (stopToken.IsCancellationRequested)
                                break;

                            foreach (var when in behavior.Whens)
                            {
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
                    }, stopToken).Unwrap();
                }

                return Result.Success();
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to initialize endpoint " + endpointName, ex);
                return Result.Failure(ex);
            }
        }

        private void scenarioContext_ContextPropertyChanged(object sender, EventArgs e)
        {
            contextChanged.Release();
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
                Logger.Error("Failed to start endpoint " + configuration.EndpointName, ex);

                return Result.Failure(ex);
            }
        }

        public Result Stop()
        {
            try
            {
                stopSource.Cancel();

                scenarioContext.ContextPropertyChanged -= scenarioContext_ContextPropertyChanged;

                executeWhens.Wait();
                contextChanged.Dispose();

                bus.Dispose();
                
                Cleanup();

                return Result.Success();
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to stop endpoint " + configuration.EndpointName, ex);

                return Result.Failure(ex);
            }
        }

        void Cleanup()
        {
            if (SettingsHolder.HasSetting("CleanupTransport"))
            {
                var transportCleaner = SettingsHolder.Get<Action>("CleanupTransport");
                transportCleaner();
            }
        }

        public override object InitializeLifetimeService()
        {
            var lease = (ILease) base.InitializeLifetimeService();
            if (lease.CurrentState == LeaseState.Initial)
            {
                lease.InitialLeaseTime = TimeSpan.FromMinutes(2);
                lease.SponsorshipTimeout = TimeSpan.FromMinutes(2);
                lease.RenewOnCallTime = TimeSpan.FromSeconds(2);
            }
            return lease;
        }

        public string Name()
        {
            return AppDomain.CurrentDomain.FriendlyName;
        }

        [Serializable]
        public class Result : MarshalByRefObject
        {
            public Exception Exception { get; set; }

            public bool Failed
            {
                get { return Exception != null; }
            }

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