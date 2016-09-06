namespace ServiceControl.Operations.BodyStorage
{
    using System.IO;
    using ServiceBus.Management.Infrastructure.Settings;

    public class FileBasedMessageBodyStore
    {
        private string rootLocation;

        public FileBasedMessageBodyStore(Settings settings)
        {
            // TODO: Make this a proper setting
            rootLocation = Directory.CreateDirectory(Path.Combine(settings.StoragePath, "MessageBodies")).FullName;
        }

        public ClaimsCheck Store(byte[] messageBody, MessageBodyMetadata messageBodyMetadata, IMessageBodyStoragePolicy messageStoragePolicy)
        {
            if (!messageStoragePolicy.ShouldStore(messageBodyMetadata))
            {
                return new ClaimsCheck(false, messageBodyMetadata);
            }

            using (var writer = new BinaryWriter(File.Open(FullPath(messageBodyMetadata.MessageId), FileMode.Create, FileAccess.Write, FileShare.None)))
            {
                writer.Write(messageBodyMetadata.MessageId);
                writer.Write(messageBodyMetadata.ContentType);
                writer.Write(messageBodyMetadata.Size);
                writer.Write(messageBody, 0, messageBody.Length);
                using(var stream = new MemoryStream(messageBody))
                {
                    stream.CopyTo(writer.BaseStream, 4096);
                }
            }

            return new ClaimsCheck(true, messageBodyMetadata);
        }

        public bool TryGet(string messageId, out byte[] messageBody, out MessageBodyMetadata messageBodyMetadata)
        {
            try
            {
                using (var file = File.Open(FullPath(messageId), FileMode.Open, FileAccess.Read, FileShare.None))
                using (var reader = new BinaryReader(file))
                {
                    // TODO: Should we verify this is the same as was passed in?
                    var messageIdFromFile = reader.ReadString();
                    var contentType = reader.ReadString();
                    var size = reader.ReadInt32();

                    messageBodyMetadata = new MessageBodyMetadata(messageIdFromFile, contentType, size);

                    // TODO: Make this buffered (and async?)
                    var body = reader.ReadBytes(size);

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

        public bool Delete(string messageId)
        {
            var path = FullPath(messageId);

            if (File.Exists(path))
            {
                File.Delete(path);
                return true;
            }

            return false;
        }

        private string FullPath(string messageId) => Path.Combine(rootLocation, messageId);
    }
}