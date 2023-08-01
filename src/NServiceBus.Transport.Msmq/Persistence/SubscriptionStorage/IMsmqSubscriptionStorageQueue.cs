namespace NServiceBus.Persistence.Msmq
{
    using System.Collections.Generic;

    interface IMsmqSubscriptionStorageQueue
    {
        IEnumerable<MsmqSubscriptionMessage> GetAllMessages();
        string Send(string body, string label);
        void TryReceiveById(string messageId);
    }
}