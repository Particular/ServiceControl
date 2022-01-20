namespace ServiceControl.Operations.BodyStorage.FileSystemStorage
{
    using System.IO;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using ServiceBus.Management.Infrastructure.Settings;


    class FileSystemBodyStorage : IBodyStorage
    {
        public FileSystemBodyStorage(Settings settings)
        {
            if (string.IsNullOrWhiteSpace(FileSystemBodyStoragePath))
            {
                FileSystemBodyStoragePath = settings.FileSystemBodyStoragePath;
            }
        }

        public Task Store(string bodyId, string contentType, int bodySize, Stream bodyStream)
        {
            var bodyFileName = GetBodyFileName(bodyId);

            if (!Directory.Exists(FileSystemBodyStoragePath))
            {
                Directory.CreateDirectory(FileSystemBodyStoragePath);
            }
            else if (File.Exists(bodyFileName))
            {
                // Short circuit on duplicate bodyId.
                return Task.CompletedTask;
            }

            if (!(bodyStream is MemoryStream memoryStream))
            {
                memoryStream = new MemoryStream();

                bodyStream.CopyTo(memoryStream);
            }

            var record = JsonConvert.SerializeObject(new BodyStorageRecord
            {
                ContentType = contentType,
                BodySize = bodySize,
                Data = memoryStream.ToArray()
            });

            File.WriteAllText(bodyFileName, record);

            return Task.CompletedTask;
        }

        public Task<StreamResult> TryFetch(string bodyId)
        {
            var bodyFileName = GetBodyFileName(bodyId);

            if (File.Exists(bodyFileName))
            {
                var lines = File.ReadAllText(bodyFileName);

                var record = JsonConvert.DeserializeObject<BodyStorageRecord>(lines);

                return Task.FromResult(new StreamResult()
                {
                    HasResult = true,
                    ContentType = record.ContentType,
                    BodySize = record.BodySize,
                    Stream = new MemoryStream(record.Data)
                });
            }
            else
            {
                return Task.FromResult(new StreamResult()
                {
                    HasResult = false,
                    Stream = null,
                });
            }
        }


        string GetBodyFileName(string bodyId)
        {
            return $"{FileSystemBodyStoragePath}{Path.DirectorySeparatorChar}{bodyId}";
        }

        static string FileSystemBodyStoragePath;

        class BodyStorageRecord
        {
            public string ContentType { get; set; }

            public int BodySize { get; set; }

            public byte[] Data { get; set; }
        }
    }
}
