namespace ServiceControl.Operations.BodyStorage
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using NServiceBus;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Contracts.Operations;

    public class StoreBody
    {
        private readonly Settings settings;
        private string auditQueuePath;
        private string errorQueuePath;
        private string bodiesPath;
        const string DefaultContentType = "text/xml";
        const int LargeObjectHeapThreshold = 85*1024;

        public StoreBody(Settings settings)
        {
            this.settings = settings;

            auditQueuePath = Directory.CreateDirectory(Path.Combine(settings.StoragePath, "queue", "audit")).FullName;
            errorQueuePath = Directory.CreateDirectory(Path.Combine(settings.StoragePath, "queue", "error")).FullName;
            bodiesPath = Directory.CreateDirectory(Path.Combine(settings.StoragePath, "bodies")).FullName;
        }

        public string AuditQueuePath => auditQueuePath;

        public string ErrorQueuePath => errorQueuePath;

        public bool SaveInStorage(TransportMessage message, out ImportSuccessfullyProcessedMessage importMessage)
        {
            importMessage = null;

            var bodySize = GetContentLength(message);
            if (bodySize >= 0 && bodySize <= settings.MaxBodySizeToStore)
            {
                return true;
            }

            importMessage = new ImportSuccessfullyProcessedMessage(message);
            importMessage.Metadata.Add("ContentLength", bodySize);

            if (bodySize == 0)
            {
                return false;
            }

            var contentType = GetContentType(message);
            importMessage.Metadata.Add("ContentType", contentType);
            importMessage.Metadata.Add("BodyNotStored", Boolean.TrueString);
            importMessage.Metadata.Add("BodyUrl", $"/messages/{importMessage.MessageId}/body_v2");

            return false;
        }

        public void SaveAuditToBeProcessedLater(TransportMessage message)
        {
            SaveToBeProcessedLater(message, AuditQueuePath);
        }

        public void SaveErrorToBeProcessedLater(TransportMessage message)
        {
            SaveToBeProcessedLater(message, ErrorQueuePath);
        }

        void SaveToBeProcessedLater(TransportMessage message, string path)
        {
            using (var writer = new BinaryWriter(File.Open(Path.Combine(path, Guid.NewGuid().ToString("N")), FileMode.Create, FileAccess.Write, FileShare.None)))
            {
                writer.Write(message.Headers.Count);
                foreach (var header in message.Headers)
                {
                    writer.Write(header.Key);
                    writer.Write(header.Value ?? String.Empty);
                }

                writer.Write(message.Body.Length);
                writer.Write(message.Body);
            }
        }

        public bool TryReadFile(string path, out Dictionary<string, string> headers, out byte[] body)
        {
            try
            {
                using (var fileStream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    using (var reader = new BinaryReader(fileStream))
                    {
                        var length = reader.ReadInt32();
                        headers = new Dictionary<string, string>(length);

                        for (var i = 0; i < length; i++)
                        {
                            headers[reader.ReadString()] = reader.ReadString();
                        }

                        body = reader.ReadBytes(reader.ReadInt32());
                    }
                }

            }
            catch (IOException)
            {
                headers = null;
                body = null;

                return false;
            }

            return true;
        }

        public async Task SaveToDB(ImportMessage importMessage)
        {
            var bodySize = GetContentLength(importMessage);
            var avoidsLargeObjectHeap = bodySize < LargeObjectHeapThreshold;

            importMessage.Metadata.Add("ContentLength", bodySize);

            var contentType = GetContentType(importMessage);
            importMessage.Metadata.Add("ContentType", contentType);

            var bodyId = importMessage.MessageId;
            var bodyUrl = $"/messages/{bodyId}/body_v2";
            var isBinary = contentType.Contains("binary");
            var body = importMessage.PhysicalMessage.Body;

            using (var stream = new FileStream(Path.Combine(bodiesPath, bodyId), FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
            {
                await stream.WriteAsync(body, 0, body.Length).ConfigureAwait(false);
            }

            if (avoidsLargeObjectHeap && !isBinary)
            {
                importMessage.Metadata.Add("Body", Encoding.UTF8.GetString(body));
            }

            importMessage.Metadata.Add("BodyUrl", bodyUrl);
        }

        static int GetContentLength(ImportMessage message)
        {
            return GetContentLength(message.PhysicalMessage.Body);
        }

        static int GetContentLength(TransportMessage message)
        {
            return GetContentLength(message.Body);
        }

        static int GetContentLength(byte[] body)
        {
            if (body == null)
            {
                return 0;
            }
            return body.Length;
        }

        static string GetContentType(ImportMessage message)
        {
            return GetContentType(message.PhysicalMessage.Headers);
        }

        static string GetContentType(TransportMessage message)
        {
            return GetContentType(message.Headers);
        }

        static string GetContentType(Dictionary<string, string> headers, string defaultContentType =  DefaultContentType)
        {
            string contentType;

            if (!headers.TryGetValue(Headers.ContentType, out contentType))
            {
                contentType = defaultContentType;
            }

            return contentType;
        }
    }
}