namespace ServiceControlInstaller.Engine.FileSystem
{
    using System;
    using System.IO;
    using System.Linq;
    using Ionic.Zip;

    public static class PlatformZipInfoExtensions
    {
        public static void ValidateZip(this PlatformZipInfo zipInfo)
        {
            if (!zipInfo.Present)
            {
                throw new FileNotFoundException($"No {zipInfo.Name} zip file found", zipInfo.FilePath);
            }

            if (!ZipFile.CheckZip(zipInfo.FilePath))
            {
                throw new Exception($"Corrupt Zip File - {zipInfo.FilePath}");
            }
        }

        public static bool TryReadReleaseDate(this PlatformZipInfo zipInfo, out DateTime releaseDate)
        {
            releaseDate = DateTime.MinValue;
            try
            {
                using (var zip = ZipFile.Read(zipInfo.FilePath))
                {
                    var entry = zip.Entries.FirstOrDefault(p => Path.GetFileName(p.FileName) == zipInfo.MainEntrypoint);
                    if (entry == null)
                    {
                        return false;
                    }

                    var tempPath = Path.GetTempPath();
                    var tempFile = Path.Combine(tempPath, entry.FileName);
                    try
                    {
                        entry.Extract(tempPath, ExtractExistingFileAction.OverwriteSilently);
                        return ReleaseDateReader.TryReadReleaseDateAttribute(tempFile, out releaseDate);
                    }
                    finally
                    {
                        File.Delete(tempFile);
                    }
                }
            }
            catch
            {
                return false;
            }
        }
    }
}