namespace ServiceControl.Persistence.Tests.AuditCapable
{
    using System;
    using ServiceControl.Configuration;

    /// <summary>
    /// A persister that exists only so tests can compose a primary host whose manifest advertises audit
    /// support, before any shipped persister does. Everything except the audit contracts is delegated to
    /// the persister named by the <see cref="InnerPersistenceTypeSetting"/> setting, so the error side of
    /// the host is the real thing. Delete it once a shipped manifest sets SupportsAuditIngestion.
    /// </summary>
    public class AuditCapableTestPersistenceConfiguration : IPersistenceConfiguration
    {
        public const string InnerPersistenceTypeSetting = "AuditCapableTestInnerPersistenceType";

        public PersistenceSettings CreateSettings(SettingsRootNamespace settingsRootNamespace) =>
            CreateInnerConfiguration(settingsRootNamespace).CreateSettings(settingsRootNamespace);

        public IPersistence Create(PersistenceSettings settings) =>
            new AuditCapableTestPersistence(CreateInnerConfiguration(PrimaryRootNamespace).Create(settings));

        static IPersistenceConfiguration CreateInnerConfiguration(SettingsRootNamespace settingsRootNamespace)
        {
            var persistenceType = SettingsReader.Read<string>(settingsRootNamespace, InnerPersistenceTypeSetting)
                ?? throw new InvalidOperationException(
                    $"The audit capable test persister needs the {settingsRootNamespace}/{InnerPersistenceTypeSetting} setting to name the persister it delegates to.");

            var manifest = PersistenceManifestLibrary.Find(persistenceType)
                ?? throw new InvalidOperationException($"No persistence manifest matches '{persistenceType}'.");

            var configurationType = Type.GetType(manifest.TypeName, throwOnError: true)!;

            return (IPersistenceConfiguration)Activator.CreateInstance(configurationType)!;
        }

        static readonly SettingsRootNamespace PrimaryRootNamespace = new("ServiceControl");
    }
}
