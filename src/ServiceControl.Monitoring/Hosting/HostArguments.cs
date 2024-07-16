namespace ServiceControl.Monitoring
{
    using System;
    using System.Collections.Generic;

    class HostArguments
    {
        public HostArguments(string[] args)
        {
            var executionMode = ExecutionMode.Run;

            var setupOptions = new OptionSet
            {
                {
                    "s|setup",
                    "Install queues",
                    s => executionMode = ExecutionMode.Setup
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

            switch (executionMode)
            {
                case ExecutionMode.Setup:
                    Commands.Add(typeof(SetupCommand));
                    break;
                case ExecutionMode.Run:
                default:
                    Commands.Add(typeof(RunCommand));
                    break;
            }
        }

        public List<Type> Commands { get; } = [];

        public string ServiceName { get; set; }

        public bool SkipQueueCreation { get; set; }

        enum ExecutionMode
        {
            Run,
            Setup
        }
    }
}