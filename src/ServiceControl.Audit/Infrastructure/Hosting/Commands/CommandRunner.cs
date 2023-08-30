namespace ServiceControl.Audit.Infrastructure.Hosting.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    class CommandRunner
    {
        public CommandRunner(List<Type> commands)
        {
            this.commands = commands;
        }

        public async Task Execute(HostArguments args, Settings.Settings settings)
        {
            foreach (var commandType in commands)
            {
                var command = (AbstractCommand)Activator.CreateInstance(commandType);
                await command.Execute(args, settings);
            }
        }

        readonly List<Type> commands;
    }
}