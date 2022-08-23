namespace ServiceControl.Monitoring
{
    using System.Threading.Tasks;
    using Infrastructure;

    class SetupCommand : AbstractCommand
    {
        public override async Task Execute(Settings settings)
        {
            await new SetupBootstrapper(settings)
                .Run()
                .ConfigureAwait(false);
        }
    }
}