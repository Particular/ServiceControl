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
        const uint VERSION = 1;
        private string rootLocation;

        public AuditIngestionCache(Settings settings)
        {
            rootLocation = Directory.CreateDirectory(Path.Combine(settings.IngestionCachePath, "audits")).FullName;
        }

        public void Write(Dictionary<string, string> headers, ClaimsCheck claimCheck)
        {
            using (var stream = File.Open(Path.Combine(rootLocation, Guid.NewGuid().ToString("N")), FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(VERSION);
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

                    stream.Flush(true);
                }
            }
        }

        public IEnumerable<string> GetBatch(int maxBatchSize) => Directory.EnumerateFiles(rootLocation).Take(maxBatchSize);

        public bool TryGet(string fileName, out Dictionary<string, string> headers, out ClaimsCheck bodyStorageClaimCheck)
        {
            try
            {
                using (var fileStream = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.None))
                using (var reader = new BinaryReader(fileStream))
                {
                    reader.ReadUInt32(); // Read version, ignore for now

                    var headersCount = reader.ReadInt32();
                    headers = new Dictionary<string, string>(headersCount);

                    for (var i = 0; i < headersCount; i++)
                    {
                        headers[reader.ReadString()] = reader.ReadString();
                    }

                    var stored = reader.ReadBoolean();
                    var messageId = reader.ReadString();
                    var contentType = reader.ReadString();
                    var size = reader.ReadInt64();

                    bodyStorageClaimCheck = new ClaimsCheck(stored, new MessageBodyMetadata(messageId, contentType, size));
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
