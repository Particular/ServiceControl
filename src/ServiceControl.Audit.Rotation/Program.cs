namespace ServiceControl.Audit.Rotation
{
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Builder;
    using System.CommandLine.Invocation;
    using System.CommandLine.Parsing;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using NServiceBus.Logging;
    using ServiceControlInstaller.Engine.Instances;

    public class RotationScheme
    {
        public string AuditQueue { get; set; }
        public List<string> Instances { get; set; }
        public TimeSpan? TimerTrigger { get; set; }
        public int? SizeTriggerMB { get; set; }
    }

    class RotationState
    {
        public int ActiveInstanceIndex { get; set; }
        public DateTime LastRotation { get; set; }
    }

    class AddInstanceCommand : Command
    {
        public AddInstanceCommand()
            : base("add", "Adds a new instance to rotation")
        {
        }
    }

    class RemoveInstanceCommand : Command
    {
        public RemoveInstanceCommand()
            : base("remove", "Removes an instance from rotation")
        {
        }
    }

    class SetUpCommand : Command
    {
        public SetUpCommand()
            : base("setup", "Sets up the rotation")
        {
            Add(new Argument<string>("queue")
            {
                Arity = new ArgumentArity(1, 1)
            });
            Add(new Argument<string>("names")
            {
                Arity = new ArgumentArity(2, ArgumentArity.MaximumArity)
            });
            Handler = CommandHandler.Create<InvocationContext, string, string[]>(
                (context, queue, names) =>
                {
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

                    var schemeJson = JsonConvert.SerializeObject(scheme, Formatting.Indented);
                    File.WriteAllText("rotation-scheme.json", schemeJson);

                    //Shut down all instances
                    foreach (var instance in instanceData.Values)
                    {
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
                        instance.ApplyConfigChange();

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

                    var stateJson = JsonConvert.SerializeObject(rotationState, Formatting.Indented);
                    File.WriteAllText("rotation-state.json", stateJson);

                    return Task.CompletedTask;
                });
        }
    }

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

    class CheckTriggersCommand : Command
    {
        public CheckTriggersCommand()
            : base("check-triggers", "Checks the triggers and performs the rotation")
        {
        }
    }

    class SetTimerTriggerCommand : Command
    {
        public SetTimerTriggerCommand()
            : base("timer", "Sets the timer trigger for rotation")
        {
        }
    }

    class SetSizeTriggerCommand : Command
    {
        public SetSizeTriggerCommand()
            : base("db-size", "Sets the database size trigger for rotation")
        {
        }


    }

    class Program
    {
        static async Task Main(string[] args)
        {
            var rootCommand = new RootCommand
            {
                new Command("instance")
                {
                    new AddInstanceCommand(),
                    new RemoveInstanceCommand()
                },
                new Command("trigger")
                {
                    new SetTimerTriggerCommand(),
                    new SetSizeTriggerCommand()
                },
                new RotateCommand(),
                new SetUpCommand(),
                new CheckTriggersCommand()
            };

            var builder = new CommandLineBuilder(rootCommand);
            builder.UseDefaults();

            var parser = builder.Build();
            await parser.InvokeAsync(args).ConfigureAwait(false);
        }
    }
}
