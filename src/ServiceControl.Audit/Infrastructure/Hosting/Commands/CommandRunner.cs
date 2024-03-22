namespace ServiceControl.Audit.Infrastructure.Hosting.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using ServiceControl.Audit.Infrastructure.Settings;

    class CommandRunner(List<Type> commands)
    {
        public async Task Execute(HostArguments args, Settings settings)
        {
            foreach (var commandType in commands)
            {
                var command = (AbstractCommand)Activator.CreateInstance(commandType);
                await command.Execute(args, settings);
            }
        }
    }
}