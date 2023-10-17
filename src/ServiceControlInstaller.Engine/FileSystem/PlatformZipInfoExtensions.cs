namespace ServiceControlInstaller.Engine.FileSystem
{
    using System;
    using Ionic.Zip;

    public static class PlatformZipInfoExtensions
    {
        public static void ValidateZip(this PlatformZipInfo zipInfo)
        {
            if (!ZipFile.CheckZip(zipInfo.FilePath))
            {
                throw new Exception($"Corrupt Zip File - {zipInfo.FilePath}");
            }
        }
    }
}