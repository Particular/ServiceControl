namespace ServiceControl.Operations.BodyStorage
{
    using System;
    using System.IO;
    using ServiceBus.Management.Infrastructure.Settings;

    public class FileBasedMessageBodyStore
    {
        const short VERSION = 1;
        private string rootLocation;

        public FileBasedMessageBodyStore(Settings settings)
        {
            rootLocation = Directory.CreateDirectory(settings.BodyStoragePath).FullName;
            Directory.CreateDirectory(Path.Combine(settings.BodyStoragePath, BodyStorageTags.ErrorTransient));
            Directory.CreateDirectory(Path.Combine(settings.BodyStoragePath, BodyStorageTags.Audit));
            Directory.CreateDirectory(Path.Combine(settings.BodyStoragePath, BodyStorageTags.ErrorPersistent));
        }

        public ClaimsCheck Store(string tag, byte[] messageBody, MessageBodyMetadata messageBodyMetadata, IMessageBodyStoragePolicy messageStoragePolicy)
        {
            if (!messageStoragePolicy.ShouldStore(messageBodyMetadata))
            {
                return new ClaimsCheck(false, messageBodyMetadata);
            }

            using (var writer = new BinaryWriter(File.Open(FullPath(tag, messageBodyMetadata.MessageId), FileMode.Create, FileAccess.Write, FileShare.None)))
            {
                writer.Write(VERSION);
                writer.Write(messageBodyMetadata.MessageId);
                writer.Write(messageBodyMetadata.ContentType);
                writer.Write(messageBodyMetadata.Size);
                writer.Write(messageBody, 0, messageBody.Length);
            }

            return new ClaimsCheck(true, messageBodyMetadata);
        }

        public bool TryGet(string tag, string messageId, out byte[] messageBody, out MessageBodyMetadata messageBodyMetadata)
        {
            try
            {
                using (var file = File.Open(FullPath(tag, messageId), FileMode.Open, FileAccess.Read, FileShare.None))
                using (var reader = new BinaryReader(file))
                {
                    reader.ReadInt16(); // ignore version for now
                    var messageIdFromFile = reader.ReadString();
                    var contentType = reader.ReadString();
                    var size = reader.ReadInt64();

                    messageBodyMetadata = new MessageBodyMetadata(messageIdFromFile, contentType, size);

                    var body = reader.ReadBytes((int)size);

                    messageBody = body;
                    return true;
                }
            }
            catch
            {
                messageBody = default(byte[]);
                messageBodyMetadata = default(MessageBodyMetadata);
                return false;
            }
        }

        public void PurgeExpired(string tag, DateTime cutOffUtc)
        {
            var tagPath = Path.Combine(rootLocation, tag);
            foreach (var file in Directory.EnumerateFiles(tagPath))
            {
                var lastTouched = File.GetLastWriteTimeUtc(file);
                if (lastTouched <= cutOffUtc)
                {
                    File.Delete(file);
                }
            }
        }

        public void ChangeTag(string messageId, string originalTag, string newTag)
        {
            var originalPath = FullPath(originalTag, messageId);
            if (File.Exists(originalPath))
            {
                var newPath = FullPath(newTag, messageId);
                File.Move(originalPath, newPath);
            }
        }

        private string FullPath(string tag, string messageId)
            => Path.Combine(rootLocation, tag, messageId);
    }
}
