namespace Particular.Backend.Debugging
{
    using System;

    public interface IStoreMessageSnapshots
    {
        void StoreOrUpdate(string uniqueId, Action<AuditMessageSnapshot> initializeNewCallback, Action<AuditMessageSnapshot> updateCallback);
        void UpdateIfExists(string uniqueId, Action<AuditMessageSnapshot> updateCallback);
    }
}