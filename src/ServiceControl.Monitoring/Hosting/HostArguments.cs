﻿namespace ServiceControl.Monitoring
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
                    s => overrides.Add(settings => { settings.ServiceName = s; })
                },
                {
                    "userName=",
                    "Username for the account the service should run under.",
                    s => overrides.Add(settings => settings.Username = s)
                },
                {
                    "skip-queue-creation",
                    "Skip queue creation during install/update",
                    s => overrides.Add(settings => settings.SkipQueueCreation = true)
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

        public void ApplyOverridesTo(Settings settings)
        {
            foreach (var @override in overrides)
            {
                @override.Invoke(settings);
            }
        }

        IList<Action<Settings>> overrides = [];

        enum ExecutionMode
        {
            Run,
            Setup
        }
    }
}