namespace ServiceControl.Audit.Infrastructure.Hosting.Commands
{
    using System;
    using System.Threading.Tasks;
    using ServiceControl.Audit.Infrastructure.Settings;

    class CommandRunner(Type commandType)
    {
        public async Task Execute(HostArguments args, Settings settings)
        {
            var command = (AbstractCommand)Activator.CreateInstance(commandType);
            await command.Execute(args, settings);
        }
    }
}