using System;
using NServiceBus;
using Raven.Client;

namespace ServiceControl.Recoverability
{
    public class RetryOperationCompletedHandler : IHandleMessages<RetryOperationCompleted>,
        IHandleMessages<AcknowledgeRetryOperationCompleted>
    {
        public IDocumentSession Session { get; set; }
        public RetryOperationManager RetryOperationManager { get; set; }

        public void Handle(AcknowledgeRetryOperationCompleted message)
        {
            RetryOperationManager.AcknowledgeCompletion(message.RequestId, message.RetryType);

            Session.Delete(CompletedRetryOperation.MakeDocumentId(message.RequestId, message.RetryType));
            Session.SaveChanges();
        }

        public void Handle(RetryOperationCompleted message)
        {
            var completedOperation = new CompletedRetryOperation
            {
                Id = CompletedRetryOperation.MakeDocumentId(message.RequestId, message.RetryType),
                RequestId = message.RequestId,
                RetryType = message.RetryType
            };

            Session.Store(completedOperation);
            Session.SaveChanges();
        }
    }
}