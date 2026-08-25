namespace ServiceControl.Hosting.Commands
{
    using System;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Configuration;
    using ServiceControl.Persistence;

    /// <summary>
    /// The startup checks both ingestion only modes share. They are deliberately driven by the
    /// persistence manifest and by settings, never by resolving optional services or by catching a
    /// startup failure, so an unsupported deployment fails with a message that names what to change.
    /// </summary>
    static class IngestionOnlyGuards
    {
        public const string SharedBodyStoragePathKey = "MessageBody/FileSystem/PathIsShared";

        const string BodyStorageTypeKey = "MessageBody/StorageType";
        const string FileSystemBodyStorage = "FileSystem";

        public static void EnsureStorageSupportsAuditIngestion(Settings settings)
        {
            var manifest = PersistenceManifestLibrary.Find(settings.PersistenceType);

            if (manifest?.SupportsAuditIngestion != true)
            {
                throw new Exception(
                    $"--audit-ingestion-only requires storage that supports audit ingestion, but this instance is configured to use '{settings.PersistenceType}'. "
                    + "Hosting audit ingestion in the primary instance is not supported for this storage type.");
            }
        }

        public static void EnsureModesAreNotCombined(bool errorIngestionOnly, bool auditIngestionOnly)
        {
            if (errorIngestionOnly && auditIngestionOnly)
            {
                throw new Exception(
                    "--error-ingestion-only and --audit-ingestion-only cannot be combined. Each queue gets its own worker pool so the two can be scaled independently, "
                    + "so run one process per mode.");
            }
        }

        /// <summary>
        /// Nothing in the file system body storage settings distinguishes a shared mount from a node
        /// local directory, so an ingestion only worker requires the operator to assert it explicitly.
        /// Without it, bodies written by a worker are unreadable by every other host.
        /// </summary>
        public static void EnsureBodyStorageIsReadableByEveryHost(string mode)
        {
            var storageType = SettingsReader.Read<string>(Settings.SettingsRootNamespace, BodyStorageTypeKey);

            if (!string.Equals(storageType, FileSystemBodyStorage, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!SettingsReader.Read(Settings.SettingsRootNamespace, SharedBodyStoragePathKey, false))
            {
                throw new Exception(
                    $"{mode} is configured for file system body storage, which every host must be able to read. "
                    + $"Set {Settings.SettingsRootNamespace}/{SharedBodyStoragePathKey} to true to assert that the configured path is a shared mount, "
                    + "or use blob or S3 body storage.");
            }
        }
    }
}
