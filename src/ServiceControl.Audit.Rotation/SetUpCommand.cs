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

    class SetUpCommand : Command
    {
        static readonly ILog Log = LogManager.GetLogger<SetUpCommand>();

        public SetUpCommand()
            : base("setup", "Sets up the rotation")
        {
            Add(new Argument<string>("queue")
            {
                Arity = new ArgumentArity(1, 1)
            });
            Add(new Argument<string>("names")
            {
                Arity = new ArgumentArity(2, 20)
            });
            Handler = CommandHandler.Create<InvocationContext, string, string[]>(
                (context, queue, names) =>
                {
                    Log.Info("Validating rotation scheme");

                    var auditInstances = InstanceFinder.ServiceControlAuditInstances();

                    var instanceData = names.ToDictionary(x => x, x =>
                    {
                        var info = auditInstances.FirstOrDefault(y => y.Name == x);
                        if (info == null)
                        {
                            throw new Exception($"Audit instance {x} does not exist.");
                        }

                        return info;
                    });

                    var scheme = new RotationScheme
                    {
                        Instances = names.ToList(),
                        AuditQueue = queue
                    };

                    Log.Info("Saving rotation scheme");

                    var schemeJson = JsonConvert.SerializeObject(scheme, Formatting.Indented);
                    File.WriteAllText("rotation-scheme.json", schemeJson);

                    //Shut down all instances
                    foreach (var instance in instanceData.Values)
                    {
                        Log.Info($"Stopping instance {instance.Name}");

                        if (!instance.TryStopService())
                        {
                            throw new Exception($"Could not stop instance {instance.Name}");
                        }
                    }

                    var activeInstance = instanceData[names[0]];
                    activeInstance.AuditQueue = queue;

                    var otherInstances = instanceData.Values.Where(x => x.Name != names[0]);
                    foreach (var instance in otherInstances)
                    {
                        instance.AuditQueue = "!disable";
                    }

                    //Apply config change and start up all instances
                    foreach (var instance in instanceData.Values)
                    {
                        Log.Info($"Updating configuration of instance {instance.Name}");

                        instance.ApplyConfigChange();

                        Log.Info($"Starting instance {instance.Name}");

                        if (!instance.TryStartService())
                        {
                            throw new Exception($"Could not start instance {instance.Name}");
                        }
                    }

                    var rotationState = new RotationState
                    {
                        ActiveInstanceIndex = 0,
                        LastRotation = DateTime.UtcNow
                    };

                    Log.Info("Saving rotation state");

                    var stateJson = JsonConvert.SerializeObject(rotationState, Formatting.Indented);
                    File.WriteAllText("rotation-state.json", stateJson);

                    return Task.CompletedTask;
                });
        }
    }
}