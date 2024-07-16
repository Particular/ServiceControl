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
                    "serviceName=",
                    "Specify the service name for the installed service.",
                    s => ServiceName = s
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

        public string ServiceName { get; private set; } = Settings.DEFAULT_SERVICE_NAME;

        public bool SkipQueueCreation { get; private set; }
    }
}