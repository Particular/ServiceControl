namespace Particular.ServiceControl.Commands
{
    using System.Threading.Tasks;
    using Hosting;
    using ServiceBus.Management.Infrastructure.Settings;

    class DatabaseMigrationsCommand : AbstractCommand
    {
        public override Task Execute(HostArguments args)
        {
            new DatabaseMigrationsBootstrapper().Run(new Settings(args.ServiceName));
            return Task.CompletedTask;
        }
    }
}