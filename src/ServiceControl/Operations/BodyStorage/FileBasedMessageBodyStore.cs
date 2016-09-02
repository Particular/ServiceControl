namespace ServiceControl.Operations.BodyStorage
{
    using System.IO;
    using ServiceBus.Management.Infrastructure.Settings;

    class FileBasedMessageBodyStore : IMessageBodyStore
    {
        private string rootLocation;

        public FileBasedMessageBodyStore(Settings settings)
        {
            // TODO: Make this a proper setting
            rootLocation = Directory.CreateDirectory(Path.Combine(settings.StoragePath, "MessageBodies")).FullName;
        }

        public ClaimsCheck Store(IMessageBody messageBody, IMessageBodyStoragePolicy messageStoragePolicy)
        {
            if (!messageStoragePolicy.ShouldStore(messageBody.Metadata))
            {
                return new ClaimsCheck(false, messageBody.Metadata);
            }

            using (var writer = new BinaryWriter(File.Open(Path.Combine(rootLocation, messageBody.Metadata.MessageId), FileMode.Create, FileAccess.Write, FileShare.None)))
            {
                writer.Write(messageBody.Metadata.MessageId);
                writer.Write(messageBody.Metadata.ContentType);
                writer.Write(messageBody.Metadata.Size);
                using (var stream = messageBody.GetBody())
                {
                    stream.CopyTo(writer.BaseStream, 4096);
                }
            }

            return new ClaimsCheck(true, messageBody.Metadata);
        }

        public bool TryGet(string messageId, out IMessageBody messageBody)
        {
            try
            {
                using (var file = File.Open(Path.Combine(rootLocation, messageId), FileMode.Open, FileAccess.Read, FileShare.None))
                using (var reader = new BinaryReader(file))
                {
                    // TODO: Should we verify this is the same as was passed in?
                    var messageIdFromFile = reader.ReadString();
                    var contentType = reader.ReadString();
                    var size = reader.ReadInt32();

                    var metadata = new MessageBodyMetadata(messageIdFromFile, contentType, size);

                    var body = reader.ReadBytes(size);

                    messageBody = new ByteArrayMessageBody(metadata, body);
                    return true;
                }
            }
            catch
            {
                messageBody = default(IMessageBody);
                return false;
            }
        }
    }
}