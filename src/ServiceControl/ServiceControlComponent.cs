
namespace Particular.ServiceControl
{
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;

    abstract class ServiceControlComponent
    {
        public abstract void Configure(Settings settings, IHostBuilder hostBuilder);
        public virtual Task Setup(Settings settings, IComponentSetupContext context)
            => Task.CompletedTask;
    }
}