namespace ServiceControl.Hosting.Commands
{
    using System;
    using System.Threading.Tasks;
    using Particular.ServiceControl.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;

    class CommandRunner(Type commandType)
    {
        public async Task Execute(HostArguments args, Settings settings)
        {
            var command = (AbstractCommand)Activator.CreateInstance(commandType);
            await command.Execute(args, settings);
        }
    }
}