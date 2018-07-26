namespace ServiceControl.Infrastructure
{
    using System;
    using System.IO;

    public class MarkerFileService
    {
        public MarkerFileService(string rootPath)
        {
            this.rootPath = rootPath;
        }

        public IDisposable CreateMarker(string name)
        {
            // normally this root path is always created by the installer
            if (!Directory.Exists(rootPath))
            {
                Directory.CreateDirectory(rootPath);
            }
            
            var path = Path.Combine(rootPath, name);
            using (File.Open(path, FileMode.OpenOrCreate))
            {
                return new MarkerFile(path);
            }
        }

        private string rootPath;

        class MarkerFile : IDisposable
        {
            public MarkerFile(string fileName)
            {
                this.fileName = fileName;
            }

            public void Dispose()
            {
                try
                {
                    File.Delete(fileName);
                }
                catch (IOException)
                {
                }
            }

            private string fileName;
        }
    }
}