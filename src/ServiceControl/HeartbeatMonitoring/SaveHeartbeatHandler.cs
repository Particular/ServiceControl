namespace ServiceControl.HeartbeatMonitoring
{
    using System;
    using System.Linq;
    using Contracts.Operations;
    using Infrastructure;
    using NServiceBus;
    using Plugin.Heartbeat.Messages;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using Raven.Json.Linq;
    using ServiceControl.Contracts.HeartbeatMonitoring;

    class SaveHeartbeatHandler : IHandleMessages<EndpointHeartbeat>
    {
        private readonly IBus bus;
        private readonly HeartbeatStatusProvider statusProvider;
        private readonly IDocumentStore store;

        public SaveHeartbeatHandler(IBus bus, HeartbeatStatusProvider statusProvider, IDocumentStore store)
        {
            this.bus = bus;
            this.statusProvider = statusProvider;
            this.store = store;
        }

        public void Handle(EndpointHeartbeat message)
        {
            if (string.IsNullOrEmpty(message.EndpointName))
            {
                throw new Exception("Received an EndpointHeartbeat message without proper initialization of the EndpointName in the schema");
            }

            if (string.IsNullOrEmpty(message.Host))
            {
                throw new Exception("Received an EndpointHeartbeat message without proper initialization of the Host in the schema");
            }

            if (message.HostId == Guid.Empty)
            {
                throw new Exception("Received an EndpointHeartbeat message without proper initialization of the HostId in the schema");
            }


            var id = DeterministicGuid.MakeId(message.EndpointName, message.HostId.ToString());
            var key = store.Conventions.DefaultFindFullDocumentKeyFromNonStringIdentifier(id, typeof(Heartbeat), false);

            var endpointDetails = new EndpointDetails
            {
                HostId = message.HostId,
                Host = message.Host,
                Name = message.EndpointName
            };

            var patchResult = store.DatabaseCommands.Patch(key, new ScriptedPatchRequest
            {
                Script = @"
if(new Date(lastReported) <= new Date(this.LastReportAt)) {
    return;
}

if(this.ReportedStatus === deadStatus) {
    output('wasDead');
}
this.LastReportAt = lastReported;
this.ReportedStatus = reportedStatus;
",
                Values =
                {
                    {"lastReported", message.ExecutedAt},
                    {"reportedStatus", (int) Status.Beating},
                    {"deadStatus", (int) Status.Dead},
                }
            }, new ScriptedPatchRequest
            {
                Script = @"
this.LastReportAt = lastReported;
this.ReportedStatus = reportedStatus;
this.EndpointDetails = {
    'Host': endpointDetails_Host,
    'HostId': endpointDetails_HostId,
    'Name': endpointDetails_Name
};
this.Disabled = false;
output('isNew');
",
                Values =
                {
                    {"lastReported", message.ExecutedAt},
                    {"reportedStatus", (int) Status.Beating},
                    {"endpointDetails_Host", endpointDetails.Host},
                    {"endpointDetails_HostId", endpointDetails.HostId.ToString()},
                    {"endpointDetails_Name", endpointDetails.Name}
                }
            }, RavenJObject.Parse(String.Format(@"
                                    {{
                                        ""Raven-Entity-Name"": ""{0}"", 
                                        ""Raven-Clr-Type"": ""{1}""
                                    }}",
                store.Conventions.GetTypeTagName(typeof(Heartbeat)),
                typeof(Heartbeat).AssemblyQualifiedName)));

            var debugStatements = patchResult.Value<RavenJArray>("Debug");
            var ravenJToken = debugStatements.SingleOrDefault();
            bool isNew = false, wasDead = false;

            if (ravenJToken != null)
            {
                var result = ravenJToken.Value<string>();
                isNew = result == "isNew";
                wasDead = result == "wasDead";
            }

            if (isNew) // New endpoint heartbeat
            {
                bus.Publish(new HeartbeatingEndpointDetected
                {
                    Endpoint = endpointDetails,
                    DetectedAt = message.ExecutedAt
                });
            }
            else if (wasDead)
            {
                bus.Publish(new EndpointHeartbeatRestored
                {
                    Endpoint = endpointDetails,
                    RestoredAt = message.ExecutedAt
                });
            }

            statusProvider.RegisterHeartbeatingEndpoint(endpointDetails, message.ExecutedAt);
        }
    }
}