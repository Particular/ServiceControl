namespace ServiceControl.Monitoring
{
    using System.Threading.Tasks;

    abstract class AbstractCommand
    {
        public abstract Task Execute(HostArguments args, Settings settings);
    }
}