namespace Particular.ServiceControl.Commands
{
    using System;
    using System.Collections.Generic;
    using Hosting;

    class CommandRunner
    {
        public CommandRunner(List<Type> commands)
        {
            this.commands = commands;
        }

        public void Execute(HostArguments args)
        {
            foreach (var commandType in commands)
            {
                var command = (AbstractCommand)Activator.CreateInstance(commandType);
                command.Execute(args);
            }
        }

        readonly List<Type> commands;
    }
}