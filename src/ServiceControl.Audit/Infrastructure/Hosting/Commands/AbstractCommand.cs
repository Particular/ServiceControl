namespace ServiceControl.Audit.Infrastructure.Hosting.Commands
{
    using System.Threading.Tasks;
    using ServiceControl.Audit.Infrastructure.Settings;

    abstract class AbstractCommand
    {
        public abstract Task Execute(HostArguments args, Settings settings);
    }
}