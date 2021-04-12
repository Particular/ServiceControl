// unset

namespace ServiceControl.Audit.Rotation
{
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.IO;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using NServiceBus.Logging;

    class SetTimerTriggerCommand : Command
    {
        static readonly ILog Log = LogManager.GetLogger<SetTimerTriggerCommand>();

        public SetTimerTriggerCommand()
            : base("timer", "Sets the timer trigger for rotation")
        {
            Add(new Argument<string>("time")
            {
                Arity = new ArgumentArity(1, 1)
            });

            Handler = CommandHandler.Create<InvocationContext, TimeSpan>(
                (context, time) =>
                {
                    if (time < TimeSpan.FromSeconds(1))
                    {
                        throw new Exception("Rotation time has to be greater than zero.");
                    }

                    Log.Info("Loading rotation scheme");

                    var schemeJson = File.ReadAllText("rotation-scheme.json");
                    var scheme = JsonConvert.DeserializeObject<RotationScheme>(schemeJson);

                    scheme.TimerTrigger = time;

                    Log.Info("Saving rotation scheme");

                    schemeJson = JsonConvert.SerializeObject(scheme, Formatting.Indented);
                    File.WriteAllText("rotation-scheme.json", schemeJson);

                    return Task.CompletedTask;
                });
        }
    }
}