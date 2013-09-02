namespace ServiceControl.Operations.Heartbeats
{
    using System.Linq;
    using Contracts.Operations;
    using EndpointPlugin.Operations.Heartbeats;
    using NServiceBus;

    public class EndpointHeartbeatHandler:IHandleMessages<EndpointHeartbeat>
    {
        public IBus Bus { get; set; }

        public void Handle(EndpointHeartbeat message)
        {
            var endpoint = Bus.CurrentMessageContext.Headers[Headers.OriginatingEndpoint];
                
            if (message.Configuration.Any())
            {
                Bus.InMemory.Raise<EndpointConfigurationReceived>(e =>
                {
                    e.Endpoint = endpoint;
                    e.SettingsReceived = message.Configuration;
                });   
                
            }

            if (message.PerformanceData.Any())
            {
                Bus.InMemory.Raise<EndpointPerformanceDataReceived>(e =>
                {
                    e.Endpoint = endpoint;
               
                    foreach (var kvp in  message.PerformanceData)
                    {
                        e.Data.Add(kvp.Key,kvp.Value.Select(dp=>new Contracts.Operations.DataPoint
                            {
                                Time = dp.Time,
                                Value = dp.Value
                            }).ToList());
                    }
                });
            }

            Bus.InMemory.Raise<EndpointHeartbeatReceived>(e =>
                {
                    e.Endpoint = endpoint;
                    e.Machine = Bus.CurrentMessageContext.Headers[Headers.OriginatingMachine];
                    e.SentAt = message.ExecutedAt;
                } );   
        }
    }
}