namespace ServiceControl.Operations.Audit
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Operations.BodyStorage;

    class AuditIngestionCache
    {
        private string rootLocation;

        public AuditIngestionCache(Settings settings)
        {
            rootLocation = Directory.CreateDirectory(Path.Combine(settings.StoragePath, "audits")).FullName;
        }

        public void Write(IDictionary<string, string> headers, ClaimsCheck claimCheck)
        {
            using (var writer = new BinaryWriter(File.Open(Path.Combine(rootLocation, Guid.NewGuid().ToString("N")), FileMode.Create, FileAccess.Write, FileShare.None)))
            {
                writer.Write(headers.Count);

                foreach (var header in headers)
                {
                    writer.Write(header.Key);
                    writer.Write(header.Value ?? string.Empty);
                }

                writer.Write(claimCheck.Stored);
                writer.Write(claimCheck.Metadata.MessageId);
                writer.Write(claimCheck.Metadata.ContentType);
                writer.Write(claimCheck.Metadata.Size);
            }
        }

        public IEnumerable<string> GetBatch(int maxBatchSize) => Directory.EnumerateFiles(rootLocation).Take(maxBatchSize);

        public bool TryGet(string fileName, out Dictionary<string, string> headers, out ClaimsCheck bodyStorageClaimCheck)
        {
            try
            {
                using (var fileStream = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    using (var reader = new BinaryReader(fileStream))
                    {
                        var length = reader.ReadInt32();
                        headers = new Dictionary<string, string>(length);

                        for (var i = 0; i < length; i++)
                        {
                            headers[reader.ReadString()] = reader.ReadString();
                        }

                        var stored = reader.ReadBoolean();
                        var messageId = reader.ReadString();
                        var contentType = reader.ReadString();
                        var size = reader.ReadInt32();

                        bodyStorageClaimCheck = new ClaimsCheck(stored, new MessageBodyMetadata(messageId, contentType, size));
                    }
                }
            }
            catch (IOException)
            {
                headers = null;
                bodyStorageClaimCheck = default(ClaimsCheck);

                return false;
            }

            return true;
        }
    }
}
