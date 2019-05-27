using System.Threading.Tasks;
using NServiceBus;
using ServiceControl.Contracts;

namespace ServiceControl.MessageAuditing
{
    public class AuditInstanceStartedHandler : IHandleMessages<AuditInstanceStarted>
    {
        public Task Handle(AuditInstanceStarted message, IMessageHandlerContext context)
        {
            return Task.CompletedTask;
        }
    }
}