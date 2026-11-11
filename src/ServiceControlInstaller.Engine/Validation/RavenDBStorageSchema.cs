namespace ServiceControlInstaller.Engine.Validation
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Reflection.Metadata;
    using System.Reflection.PortableExecutable;

    // The storage versions a RavenDB server upgrades existing data to when it opens it, as declared in Raven.Server.dll.
    public record RavenDBStorageSchema(int Server, int Configuration, int Documents, int LuceneIndex, int CoraxIndex)
    {
        // Server and configuration storage are small. Document and index storage grow with the data, and so does the time to upgrade them.
        public bool RequiresDataUpgradeFrom(RavenDBStorageSchema installed) =>
            installed.Documents != Documents || installed.LuceneIndex != LuceneIndex || installed.CoraxIndex != CoraxIndex;

        public static RavenDBStorageSchema Bundled => bundled.Value;

        public static bool RequiresDataUpgrade(string installPath) =>
            ReadFromInstance(installPath) is { } installed && Bundled is { } target && target.RequiresDataUpgradeFrom(installed);

        public static RavenDBStorageSchema ReadFromInstance(string installPath)
        {
            var path = Path.Combine(installPath, "Persisters", "RavenDB", "RavenDBServer", ServerAssemblyName);
            if (!File.Exists(path))
            {
                return null;
            }

            using var stream = File.OpenRead(path);
            return Read(stream);
        }

        internal static RavenDBStorageSchema ReadFromBundledServer()
        {
            using var zipStream = typeof(RavenDBStorageSchema).Assembly.GetManifestResourceStream("RavenDBServer.zip");
            if (zipStream == null)
            {
                return null;
            }

            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
            var entry = archive.GetEntry(ServerAssemblyName);
            if (entry == null)
            {
                return null;
            }

            // PEReader needs a seekable stream, and zip entry streams are not.
            using var assembly = new MemoryStream();
            using (var entryStream = entry.Open())
            {
                entryStream.CopyTo(assembly);
            }

            assembly.Position = 0;
            return Read(assembly);
        }

        internal static RavenDBStorageSchema Read(Stream assembly)
        {
            using var peReader = new PEReader(assembly, PEStreamOptions.LeaveOpen);
            if (!peReader.HasMetadata)
            {
                return null;
            }

            var metadata = peReader.GetMetadataReader();
            foreach (var handle in metadata.TypeDefinitions)
            {
                var type = metadata.GetTypeDefinition(handle);
                if (!metadata.StringComparer.Equals(type.Name, "CurrentVersion") || type.GetDeclaringType().IsNil)
                {
                    continue;
                }

                var declaringType = metadata.GetTypeDefinition(type.GetDeclaringType());
                if (!metadata.StringComparer.Equals(declaringType.Name, "SchemaUpgrader") || !metadata.StringComparer.Equals(declaringType.Namespace, "Raven.Server.Storage.Schema"))
                {
                    continue;
                }

                var versions = ReadInt32Constants(metadata, type);
                return versions.TryGetValue("ServerVersion", out var server)
                       && versions.TryGetValue("ConfigurationVersion", out var configuration)
                       && versions.TryGetValue("DocumentsVersion", out var documents)
                       && versions.TryGetValue("LuceneIndexVersion", out var luceneIndex)
                       && versions.TryGetValue("CoraxIndexVersion", out var coraxIndex)
                    ? new RavenDBStorageSchema(server, configuration, documents, luceneIndex, coraxIndex)
                    : null;
            }

            return null;
        }

        static Dictionary<string, int> ReadInt32Constants(MetadataReader metadata, TypeDefinition type)
        {
            var constants = new Dictionary<string, int>();
            foreach (var fieldHandle in type.GetFields())
            {
                var field = metadata.GetFieldDefinition(fieldHandle);
                var constantHandle = field.GetDefaultValue();
                if (constantHandle.IsNil)
                {
                    continue;
                }

                var constant = metadata.GetConstant(constantHandle);
                if (constant.TypeCode == ConstantTypeCode.Int32)
                {
                    constants[metadata.GetString(field.Name)] = metadata.GetBlobReader(constant.Value).ReadInt32();
                }
            }

            return constants;
        }

        const string ServerAssemblyName = "Raven.Server.dll";

        static readonly Lazy<RavenDBStorageSchema> bundled = new(ReadFromBundledServer);
    }
}
