namespace ServiceControlInstaller.Engine.FileSystem
{
    using System;
    using System.IO;
    using Ionic.Zip;

    public static class PlatformZipInfoExtensions
    {
        public static void ValidateZip(this PlatformZipInfo zipInfo)
        {
            if (!zipInfo.Present)
            {
                throw new FileNotFoundException($"No Zip file found at: {zipInfo.FilePath}");
            }

            if (!ZipFile.CheckZip(zipInfo.FilePath))
            {
                throw new Exception($"Corrupt Zip File - {zipInfo.FilePath}");
            }
        }
    }
}