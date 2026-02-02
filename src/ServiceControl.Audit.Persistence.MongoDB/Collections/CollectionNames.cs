namespace ServiceControl.Audit.Persistence.MongoDB.Collections
{
    /// <summary>
    /// Constants for MongoDB collection names.
    /// </summary>
    static class CollectionNames
    {
        public const string ProcessedMessages = "processedMessages";
        public const string KnownEndpoints = "knownEndpoints";
        public const string SagaSnapshots = "sagaSnapshots";
        public const string FailedAuditImports = "failedAuditImports";
        public const string MessageBodies = "messageBodies";
    }
}
