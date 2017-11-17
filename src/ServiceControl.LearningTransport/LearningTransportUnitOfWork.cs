namespace ServiceControl.LearningTransport
{
    using System;
    using System.Threading;

    class LearningTransportUnitOfWork : IDisposable
    {
        public ILearningTransportTransaction Transaction => currentTransaction.Value;

        public void SetTransaction(ILearningTransportTransaction transaction)
        {
            currentTransaction.Value = transaction;
        }

        public bool HasActiveTransaction
        {
            get
            {
                if (!currentTransaction.IsValueCreated)
                {
                    return false;
                }

                return currentTransaction.Value != null;
            }
        }

        public void ClearTransaction()
        {
            currentTransaction.Value = null;
        }

        public void Dispose()
        {
            //Injected
        }

        ThreadLocal<ILearningTransportTransaction> currentTransaction = new ThreadLocal<ILearningTransportTransaction>();
    }
}
