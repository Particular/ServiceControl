namespace ServiceControl.Infrastructure
{
    using System;
    using System.IO;

    public class MarkerFileService
    {
        private string rootPath;
        public MarkerFileService(string rootPath)
        {
            this.rootPath = rootPath;
        }

        public IDisposable CreateMarker(string name)
        {
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

        class MarkerFile : IDisposable
        {
            private string fileName;

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
        }
    }
}
