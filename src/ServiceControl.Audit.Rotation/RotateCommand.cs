namespace ServiceControl.Audit.Rotation
{
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using NServiceBus.Logging;
    using ServiceControlInstaller.Engine.Instances;

    class RotateCommand : Command
    {
        static readonly ILog Log = LogManager.GetLogger<RotateCommand>();

        public RotateCommand()
            : base("rotate", "Forces the rotation")
        {
            Handler = CommandHandler.Create<InvocationContext>(
                context =>
                {
                    Log.Info("Loading rotation scheme");

                    var schemeJson = File.ReadAllText("rotation-scheme.json");
                    var scheme = JsonConvert.DeserializeObject<RotationScheme>(schemeJson);

                    Log.Info("Loading rotation state");

                    var stateJson = File.ReadAllText("rotation-state.json");
                    var rotationState = JsonConvert.DeserializeObject<RotationState>(stateJson);

                    Log.Info("Detecting installed audit instances");

                    var auditInstances = InstanceFinder.ServiceControlAuditInstances();

                    Log.Info("Validating state");

                    var instanceData = scheme.Instances.ToDictionary(x => x, x =>
                    {
                        var info = auditInstances.FirstOrDefault(y => y.Name == x);
                        if (info == null)
                        {
                            throw new Exception($"Audit instance {x} does not exist.");
                        }

                        return info;
                    });

                    var activeInstanceName = scheme.Instances[rotationState.ActiveInstanceIndex];
                    var activeInstance = instanceData[activeInstanceName];

                    //Validate
                    if (activeInstance.AuditQueue != scheme.AuditQueue)
                    {
                        throw new Exception(
                            $"The instance information is out of sync with the rotation scheme. Active instance queue is {activeInstance.AuditQueue} but should be {scheme.AuditQueue}.");
                    }

                    var otherInstances = instanceData.Values.Where(x => x.Name != activeInstanceName);
                    foreach (var instance in otherInstances)
                    {
                        if (instance.AuditQueue != "!disable")
                        {
                            throw new Exception(
                                $"The instance information is out of sync with the rotation scheme. Inactive instance {instance.Name} is bound to the audit queue {instance.AuditQueue}.");
                        }
                    }

                    var trailingInstanceIndex = rotationState.ActiveInstanceIndex - 1;
                    if (trailingInstanceIndex < 0)
                    {
                        trailingInstanceIndex = scheme.Instances.Count - 1;
                    }


                    var trailingInstanceName = scheme.Instances[trailingInstanceIndex];
                    var trailingInstance = instanceData[trailingInstanceName];

                    Log.Info($"Stopping instance {trailingInstanceName}");

                    if (!trailingInstance.TryStopService())
                    {
                        throw new Exception($"Count not stop instance {trailingInstance.Name}");
                    }

                    Log.Info($"Erasing instance {trailingInstanceName}");

                    trailingInstance.RemoveDataBaseFolder();

                    Log.Info($"Switching instance {trailingInstanceName} to active mode");

                    trailingInstance.AuditQueue = scheme.AuditQueue;
                    trailingInstance.ApplyConfigChange();

                    Log.Info($"Stopping instance {activeInstanceName}");

                    //Detach the active instance
                    if (!activeInstance.TryStopService())
                    {
                        throw new Exception($"Count not stop instance {activeInstance.Name}");
                    }

                    Log.Info($"Switching instance {activeInstanceName} to passive mode");

                    activeInstance.AuditQueue = "!disable";
                    activeInstance.ApplyConfigChange();

                    Log.Info($"Starting instance {activeInstanceName}");

                    if (!activeInstance.TryStartService())
                    {
                        throw new Exception($"Count not start instance {activeInstance.Name}");
                    }

                    Log.Info($"Starting instance {trailingInstanceName}");

                    //Start trailing instance (new active instance)
                    if (!trailingInstance.TryStartService())
                    {
                        throw new Exception($"Count not start instance {trailingInstance.Name}");
                    }

                    Log.Info("Saving rotation state");

                    rotationState = new RotationState
                    {
                        ActiveInstanceIndex = trailingInstanceIndex,
                        LastRotation = DateTime.UtcNow
                    };

                    stateJson = JsonConvert.SerializeObject(rotationState, Formatting.Indented);
                    File.WriteAllText("rotation-state.json", stateJson);

                    return Task.CompletedTask;
                });
        }
    }
}