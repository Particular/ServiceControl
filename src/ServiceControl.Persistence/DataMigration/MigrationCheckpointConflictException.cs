namespace ServiceControl.Persistence.DataMigration;

using System;

/// <summary>Thrown when a checkpoint save carries a version the stored row no longer holds, because another writer moved it on.</summary>
public sealed class MigrationCheckpointConflictException(string message, Exception? innerException = null) : Exception(message, innerException);
