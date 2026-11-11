namespace ServiceControlInstaller.Engine.Instances
{
    using System.IO;
    using FileSystem;

    // A 7.2 server converts 6.2 data one way, so only RavenDB7 gets it; SQL persisters keep the 6.2 server the RavenDB to SQL migration reads.
    static class RavenDBServerPackage
    {
        public const string RavenDBArchive = "RavenDBServer.zip";
        public const string RavenDB7Archive = "RavenDB7Server.zip";

        public static bool IsRavenDBPersister(PersistenceManifest persistence) =>
            persistence is { Name: StorageEngineNames.RavenDB or StorageEngineNames.RavenDB7 };

        public static string ArchiveResourceName(PersistenceManifest persistence) =>
            UsesRavenDB7Server(persistence) ? RavenDB7Archive : RavenDBArchive;

        public static string ServerDirectory(string installPath, PersistenceManifest persistence) =>
            Path.Combine(installPath, "Persisters", UsesRavenDB7Server(persistence) ? StorageEngineNames.RavenDB7 : StorageEngineNames.RavenDB, "RavenDBServer");

        public static void Install(string installPath, PersistenceManifest persistence) =>
            FileUtils.UnzipToSubdirectory(ArchiveResourceName(persistence), ServerDirectory(installPath, persistence));

        public static bool UsesRavenDB7Server(PersistenceManifest persistence) => persistence is { Name: StorageEngineNames.RavenDB7 };
    }
}
