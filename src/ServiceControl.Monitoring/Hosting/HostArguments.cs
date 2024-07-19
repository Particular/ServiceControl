namespace ServiceControl.Monitoring
{
    using System;

    class HostArguments
    {
        public HostArguments(string[] args)
        {
            var setupOptions = new OptionSet
            {
                {
                    "s|setup",
                    "Install queues",
                    s => Command = typeof(SetupCommand)
                },
                {
                    "skip-queue-creation",
                    "Skip queue creation during install/update",
                    s => SkipQueueCreation = true
                }
            };

            setupOptions.Parse(args);
        }

        public Type Command { get; private set; } = typeof(RunCommand);

        public bool SkipQueueCreation { get; private set; }
    }
}