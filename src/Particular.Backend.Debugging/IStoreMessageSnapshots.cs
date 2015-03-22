namespace Particular.Backend.Debugging
{
    using System;

    public interface IStoreMessageSnapshots
    {
        void StoreOrUpdate(string uniqueId, Action<MessageSnapshot> initializeNewCallback, Action<MessageSnapshot> updateCallback);
        void UpdateIfExists(string uniqueId, Action<MessageSnapshot> updateCallback);
    }
}