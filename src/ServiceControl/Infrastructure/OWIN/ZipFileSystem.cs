namespace ServiceBus.Management.Infrastructure.OWIN
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using Microsoft.Owin.FileSystems;

    class ZipFileSystem : IFileSystem
    {
        ZipArchive archive;

        public ZipFileSystem(string path)
        {
            archive = ZipFile.OpenRead(path);
        }

        public bool TryGetFileInfo(string subpath, out IFileInfo fileInfo)
        {
            if (subpath.Length > 1)
            {
                var entry = archive.GetEntry(subpath.Substring(1));
                if (entry != null)
                {
                    fileInfo = new ZipEntryFileInfo(entry);
                    return true;
                }
            }
            fileInfo = null;
            return false;
        }

        public bool TryGetDirectoryContents(string subpath, out IEnumerable<IFileInfo> contents)
        {
            contents = Enumerable.Empty<IFileInfo>();
            return true;
        }

        class ZipEntryFileInfo : IFileInfo
        {
            readonly ZipArchiveEntry entry;

            public ZipEntryFileInfo(ZipArchiveEntry entry)
            {
                this.entry = entry;
            }

            public Stream CreateReadStream() => entry.Open();

            public long Length => entry.Length;
            public string PhysicalPath => null;
            public string Name => entry.Name;
            public DateTime LastModified => entry.LastWriteTime.DateTime;
            public bool IsDirectory => false;
        }
    }
}