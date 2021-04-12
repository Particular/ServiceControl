namespace ServiceControl.Audit.Rotation
{
    using System.CommandLine;
    using System.CommandLine.Builder;
    using System.CommandLine.Parsing;
    using System.Threading.Tasks;

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

    class CheckTriggersCommand : Command
    {
        public CheckTriggersCommand()
            : base("check-triggers", "Checks the triggers and performs the rotation")
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
                new CheckTriggersCommand(),
                new PrintCommand()
            };

            var builder = new CommandLineBuilder(rootCommand);
            builder.UseDefaults();

            var parser = builder.Build();
            await parser.InvokeAsync(args).ConfigureAwait(false);
        }
    }
}
