namespace NServiceBus
{
    using Features;
    using Persistence;
    using Persistence.Msmq;

    /// <summary>
    /// Used to enable Msmq persistence.
    /// </summary>
    public class MsmqPersistence : PersistenceDefinition
    {
        internal MsmqPersistence()
        {
            Supports<StorageType.Subscriptions>(s =>
            {
                s.EnableFeatureByDefault<MsmqSubscriptionPersistence>();
            });
        }
    }
}