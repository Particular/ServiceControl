namespace Particular.ServiceControl.Commands
{
	using Particular.ServiceControl.Hosting;
	using ServiceBus.Management.Infrastructure.Settings;

	class DatabaseMigrationsCommand : AbstractCommand
	{
		public override void Execute(HostArguments args)
		{
			new DatabaseMigrationsBootstrapper().Run(new Settings(args.ServiceName));
		}
	}
}