namespace TestDataGenerator
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus;

    public class EndpointInfo
    {
        bool sendOnly;

        public EndpointInfo(string name, bool sendOnly = false)
        {
            Name = name;
            this.sendOnly = sendOnly;
            Context = new EndpointContext(name);
        }

        public string Name { get; }
        public IEndpointInstance Instance { get; private set; }
        public EndpointContext Context { get; }
        public string Status => (Instance != null) ? "Running" : "Stopped";

        public async Task Start()
        {
            if (Instance != null)
            {
                return;
            }

            var cfg = new EndpointConfiguration(Name);

            var transport = cfg.UseTransport<LearningTransport>();
            transport.StorageDirectory(@"C:\tmp");

            var persistence = cfg.UsePersistence<LearningPersistence>();
            persistence.SagaStorageDirectory(@"C:\tmp");

            cfg.UseSerialization<NewtonsoftJsonSerializer>();

            cfg.EnableInstallers();

            cfg.Recoverability().Immediate(x => x.NumberOfRetries(0)).Delayed(x => x.NumberOfRetries(0));

            cfg.RegisterComponents(registration => registration.RegisterSingleton(Context));

            var platformConfig = ServicePlatformConnectionConfiguration.Parse(@"{
  ""ErrorQueue"": ""error"",
  ""Heartbeats"": {
    ""Enabled"": true,
    ""HeartbeatsQueue"": ""Particular.ServiceControl""
  },
  ""CustomChecks"": {
    ""Enabled"": true,
    ""CustomChecksQueue"": ""Particular.ServiceControl""
  },
  ""MessageAudit"": {
    ""Enabled"": true,
    ""AuditQueue"": ""audit""
  },
  ""SagaAudit"": {
    ""Enabled"": true,
    ""SagaAuditQueue"": ""audit""
  },
  ""Metrics"": {
    ""Enabled"": true,
    ""MetricsQueue"": ""Particular.Monitoring"",
    ""Interval"": ""00:00:10""
  }
}");

            if (sendOnly)
            {
                cfg.SendOnly();
                platformConfig.Metrics.Enabled = false;
            }


            cfg.ConnectToServicePlatform(platformConfig);

            Instance = await Endpoint.Start(cfg);
        }

        public async Task Stop()
        {
            if (Instance != null)
            {
                await Instance.Stop();
                Instance = null;
            }
        }

        public override string ToString()
        {
            return $"{Name,-10}  {Status,-7}  {Context.SimpleMessagesReceived,12}  {string.Join(",", GetStatusFlags())}";
        }

        IEnumerable<string> GetStatusFlags()
        {
            if (Context.ThrowExceptions)
            {
                yield return "ThrowingExceptions";
            }
            if (Context.FailCustomCheck)
            {
                yield return "FailCustomChecks";
            }

            if (!Context.ThrowExceptions && !Context.FailCustomCheck)
            {
                yield return "Normal";
            }
        }
    }
}
