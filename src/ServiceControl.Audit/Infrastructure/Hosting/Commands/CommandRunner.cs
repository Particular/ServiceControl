namespace Particular.ServiceControl.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Hosting;

    class CommandRunner
    {
        public CommandRunner(List<Type> commands)
        {
            this.commands = commands;
        }

        public async Task Execute(HostArguments args)
        {
            foreach (var commandType in commands)
            {
                var command = (AbstractCommand)Activator.CreateInstance(commandType);
                await command.Execute(args).ConfigureAwait(false);
            }
        }

        readonly List<Type> commands;
    }
}