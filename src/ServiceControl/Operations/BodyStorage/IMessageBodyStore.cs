namespace ServiceControl.Operations.BodyStorage
{
    using System;
    using System.IO;
    using ServiceControl.Operations.BodyStorage.RavenAttachments;

    static class BodyStorageTags
    {
        public static string ErrorTransient = "error-transient";
        public static string ErrorPersistent = "error-persistent";
        public static string Audit = "audit";
    }

    public interface IMessageBodyStore
    {
        ClaimsCheck Store(string tag, byte[] messageBody, MessageBodyMetadata messageBodyMetadata, IMessageBodyStoragePolicy messageStoragePolicy);
        bool TryGet(string tag, string messageId, out byte[] messageBody, out MessageBodyMetadata messageBodyMetadata);
        void Delete(string tag, string messageId);
    }

    class BackwardsCompatibleMessageBodyStore : IMessageBodyStore
    {
        private FileBasedMessageBodyStore newMessageBodyStore;
        private RavenAttachmentsBodyStorage legacyBodyStorage;

        public BackwardsCompatibleMessageBodyStore(FileBasedMessageBodyStore newMessageBodyStore, RavenAttachmentsBodyStorage legacyBodyStorage)
        {
            this.newMessageBodyStore = newMessageBodyStore;
            this.legacyBodyStorage = legacyBodyStorage;
        }

        public ClaimsCheck Store(string tag, byte[] messageBody, MessageBodyMetadata messageBodyMetadata, IMessageBodyStoragePolicy messageStoragePolicy)
        {
            return newMessageBodyStore.Store(tag, messageBody, messageBodyMetadata, messageStoragePolicy);
        }

        public bool TryGet(string tag, string messageId, out byte[] messageBody, out MessageBodyMetadata messageBodyMetadata)
        {
            if (newMessageBodyStore.TryGet(tag, messageId, out messageBody, out messageBodyMetadata))
            {
                return true;
            }

            Stream stream = null;

            try
            {
                string contentType;
                long contentLength;
                if (legacyBodyStorage.TryFetch(messageId, out stream, out contentType, out contentLength))
                {
                    messageBody = ReadFully(stream);
                    messageBodyMetadata = new MessageBodyMetadata(messageId, contentType, contentLength);

                    return true;
                }
            }
            finally
            {
                stream?.Dispose();
            }

            return false;
        }

        private static byte[] ReadFully(Stream input)
        {
            var buffer = new byte[16 * 1024];
            using (var ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }

        public void Delete(string tag, string messageId)
        {
            if (!newMessageBodyStore.Delete(tag, messageId))
            {
                legacyBodyStorage.Delete(messageId);
            }
        }
        public void PurgeExpired(string tag, DateTime cutOffUtc)
            => newMessageBodyStore.PurgeExpired(tag, cutOffUtc);
    }
}