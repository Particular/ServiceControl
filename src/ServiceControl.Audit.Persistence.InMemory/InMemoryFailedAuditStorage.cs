namespace ServiceControl.Audit.Persistence.InMemory
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Audit.Auditing;

    class InMemoryFailedAuditStorage : IFailedAuditStorage
    {
        public async Task ProcessFailedMessages(Func<FailedTransportMessage, Func<CancellationToken, Task>, CancellationToken, Task> onMessage, CancellationToken cancellationToken)
        {
            foreach (var failedMessage in failedMessages)

            {
                FailedTransportMessage transportMessage = failedMessage.Message;

                await onMessage(transportMessage, (_) => { return Task.CompletedTask; }, cancellationToken).ConfigureAwait(false);
            }

            failedMessages.Clear();
        }


        public Task Store(dynamic failure)
        {
            failedMessages.Add(failure);

            return Task.CompletedTask;
        }

        List<dynamic> failedMessages = new List<dynamic>();
    }
}