namespace NServiceBus.Transport.Msmq
{
    using System;
    using System.Collections.Generic;
    using MSMQ.Messaging;
    using System.Threading.Tasks;
    using NServiceBus.Extensibility;
    using Transport;

    class SendsAtomicWithReceiveNativeTransactionStrategy : ReceiveStrategy
    {
        public SendsAtomicWithReceiveNativeTransactionStrategy(MsmqFailureInfoStorage failureInfoStorage)
        {
            this.failureInfoStorage = failureInfoStorage;
        }

        public override async Task ReceiveMessage()
        {
            Message message = null;

            try
            {
                using (var msmqTransaction = new MessageQueueTransaction())
                {
                    msmqTransaction.Begin();

                    if (!TryReceive(msmqTransaction, out message))
                    {
                        return;
                    }

                    if (!TryExtractHeaders(message, out var headers))
                    {
                        MovePoisonMessageToErrorQueue(message, msmqTransaction);

                        msmqTransaction.Commit();
                        return;
                    }

                    var shouldCommit = await ProcessMessage(msmqTransaction, message, headers).ConfigureAwait(false);

                    if (shouldCommit)
                    {
                        msmqTransaction.Commit();
                        failureInfoStorage.ClearFailureInfoForMessage(message.Id);
                    }
                    else
                    {
                        msmqTransaction.Abort();
                    }
                }
            }
            // We'll only get here if Commit/Abort/Dispose throws which should be rare.
            // Note: If that happens the attempts counter will be inconsistent since the message might be picked up again before we can register the failure in the LRU cache.
            catch (Exception exception)
            {
                if (message == null)
                {
                    throw;
                }

                failureInfoStorage.RecordFailureInfoForMessage(message.Id, exception);
            }
        }

        async Task<bool> ProcessMessage(MessageQueueTransaction msmqTransaction, Message message, Dictionary<string, string> headers)
        {
            var transportTransaction = new TransportTransaction();

            transportTransaction.Set(msmqTransaction);

            if (failureInfoStorage.TryGetFailureInfoForMessage(message.Id, out var failureInfo))
            {
                var errorHandleResult = await HandleError(message, failureInfo.Exception, transportTransaction, failureInfo.NumberOfProcessingAttempts).ConfigureAwait(false);

                if (errorHandleResult == ErrorHandleResult.Handled)
                {
                    return true;
                }
            }

            try
            {
                using (var bodyStream = message.BodyStream)
                {
                    var context = new ContextBag();
                    context.Set(message);

                    var shouldAbortMessageProcessing = await TryProcessMessage(message.Id, headers, bodyStream, transportTransaction, context).ConfigureAwait(false);

                    if (shouldAbortMessageProcessing)
                    {
                        return false;
                    }
                }
                return true;
            }
            catch (Exception exception)
            {
                failureInfoStorage.RecordFailureInfoForMessage(message.Id, exception);

                return false;
            }
        }

        MsmqFailureInfoStorage failureInfoStorage;
    }
}