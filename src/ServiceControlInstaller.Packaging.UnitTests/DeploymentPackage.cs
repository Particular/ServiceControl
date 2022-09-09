namespace Tests
{
    using System;
    using System.Collections;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using NUnit.Framework;

    public class DeploymentPackage
    {
        FileInfo zipFile;

        public DeploymentPackage(FileInfo zipFile)
        {
            this.zipFile = zipFile;
            ServiceName = zipFile.Name
                .Replace("Particular.", "")
                .Split('-')
                .First();
        }

        public override string ToString() => ServiceName.Replace(".", " ");
        public string ServiceName { get; }
        public string FullName => zipFile.FullName;
        public ZipArchive Open() => ZipFile.OpenRead(FullName);

        public static IEnumerable All => GetZipFolder()
            .EnumerateFiles("*.zip")
            .Select(x => new DeploymentPackage(x));

        public static DirectoryInfo GetZipFolder()
        {
            var currentFolder = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);

            while (currentFolder != null)
            {
                foreach (var folder in currentFolder.EnumerateDirectories("zip", SearchOption.TopDirectoryOnly))
                {
                    return folder;
                }

                currentFolder = currentFolder.Parent;
            }

            throw new Exception("Cannot find zip folder");
        }
    }
}