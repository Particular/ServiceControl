namespace ServiceControl.Operations.BodyStorage
{
    using System.IO;
    using ServiceBus.Management.Infrastructure.Settings;

    public class StoreBody
    {
        private string bodiesPath;

        public StoreBody(Settings settings)
        {
            bodiesPath = Directory.CreateDirectory(settings.BodyStoragePath).FullName;
        }

        public bool TryRetrieveBody(string bodyId, out Stream stream, out string contentType, out long contentLength)
        {
            var path = Path.Combine(bodiesPath, bodyId);

            contentType = null;
            contentLength = 0;

            if (File.Exists(path))
            {
                stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
                contentLength = stream.Length;
                return true;
            }

            stream = null;
            return false;
        }
    }
}
