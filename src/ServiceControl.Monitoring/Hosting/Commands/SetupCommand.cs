namespace ServiceControl.Monitoring
{
    using System.Threading.Tasks;
    using Infrastructure;

    class SetupCommand : AbstractCommand
    {
        public override async Task Execute(Settings settings, HostArguments args)
        {
            await new SetupBootstrapper(settings)
                .Run()
                .ConfigureAwait(false);
        }
    }
}