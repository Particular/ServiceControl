namespace ServiceControl.Operations.BodyStorage.AzureBlobStorage
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Azure.Storage.Blobs;
    using ServiceBus.Management.Infrastructure.Settings;

    class AzureBlobBodyStorage : IBodyStorage
    {
        readonly string blobStorageConnectionString;
        readonly string blobStorageContainerName;

        BlobServiceClient blobStorageClient;
        BlobContainerClient blobContainerClient;


        public AzureBlobBodyStorage(Settings settings)
        {
            blobStorageConnectionString = Environment.GetEnvironmentVariable(settings.BlobStorageConnectionStringEnvironmentVariable);
            blobStorageContainerName = settings.BlobStorageContainerName;
        }

        public async Task Store(string bodyId, string contentType, int bodySize, Stream bodyStream)
        {
            try
            {
                var blob = await GetBlobClientAsync(bodyId).ConfigureAwait(false);

                if (await blob.ExistsAsync().ConfigureAwait(false))
                {
                    return;
                }

                await blob.UploadAsync(bodyStream).ConfigureAwait(false);

                blob.SetMetadata(new Dictionary<string, string>()
                    {
                        { "ContentType", contentType },
                        { "BodySize", bodySize.ToString()}
                    });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write(ex.ToString());
                throw;
            }
        }

        public async Task<StreamResult> TryFetch(string bodyId)
        {
            var blob = await GetBlobClientAsync(bodyId).ConfigureAwait(false);

            if (await blob.ExistsAsync().ConfigureAwait(false))
            {
                var props = await blob.GetPropertiesAsync().ConfigureAwait(false);

                var data = new MemoryStream();
                await blob.DownloadToAsync(data).ConfigureAwait(false);
                data.Position = 0;

                return new StreamResult
                {
                    HasResult = true,
                    ContentType = props.Value.Metadata["ContentType"],
                    BodySize = int.Parse(props.Value.Metadata["BodySize"]),
                    Stream = data
                };
            }

            return new StreamResult
            {
                HasResult = false,
                BodySize = 0,
                ContentType = null,
                Stream = null
            };
        }

        async Task<BlobClient> GetBlobClientAsync(string bodyId)
        {
            var container = await GetBlobContainer().ConfigureAwait(false);
            var blob = container.GetBlobClient(bodyId);

            return blob;
        }

        async Task<BlobContainerClient> GetBlobContainer()
        {
            if (blobContainerClient == null)
            {
                blobContainerClient = BlobStorageClient.GetBlobContainerClient(blobStorageContainerName);
                await blobContainerClient.CreateIfNotExistsAsync().ConfigureAwait(false);
            }

            return blobContainerClient;
        }

        BlobServiceClient BlobStorageClient
        {
            get
            {
                if (blobStorageClient == null)
                {
                    blobStorageClient = new BlobServiceClient(blobStorageConnectionString);
                }

                return blobStorageClient;
            }
        }
    }
}
