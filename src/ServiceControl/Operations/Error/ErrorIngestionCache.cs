namespace ServiceControl.Operations.Error
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Operations.BodyStorage;

    class ErrorIngestionCache
    {
        const short VERSION = 1;
        private string rootLocation;

        public ErrorIngestionCache(Settings settings)
        {
            rootLocation = Directory.CreateDirectory(Path.Combine(settings.IngestionCachePath, "errors")).FullName;
        }

        public void Write(IDictionary<string, string> headers, bool recoverable, ClaimsCheck claimCheck)
        {
            using (var writer = new BinaryWriter(File.Open(Path.Combine(rootLocation, Guid.NewGuid().ToString("N")), FileMode.Create, FileAccess.Write, FileShare.None)))
            {
                writer.Write(VERSION);
                writer.Write(recoverable);
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

        public bool TryGet(string fileName, out Dictionary<string, string> headers, out bool recoverable, out ClaimsCheck bodyStorageClaimCheck)
        {
            try
            {
                using (var fileStream = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.None))
                using (var reader = new BinaryReader(fileStream))
                {
                    reader.ReadInt16(); // Read version, ignore for now

                    recoverable = reader.ReadBoolean();

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
                recoverable = false;

                return false;
            }

            return true;
        }
    }
}
