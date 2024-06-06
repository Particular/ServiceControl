namespace ServiceControlInstaller.Engine.FileSystem
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Security.AccessControl;
    using Ionic.Zip;

    static class FileUtils
    {
        public static string SanitizeFolderName(string folderName)
        {
            return Path.GetInvalidPathChars().Aggregate(folderName, (current, c) => current.Replace(c, '-'));
        }

        public static void DeleteDirectory(string path, bool recursive, bool contentsOnly, params string[] excludes)
        {
            var di = new DirectoryInfo(path);

            if (!di.Exists)
            {
                return;
            }

            var originalPath = path;
            var originalAttributes = di.Attributes;

            // Move folder to ensure no files are in use so that it is less likely that we corrupt the folder
            // when still in use

            var pathToDelete = path + "$";
            di.MoveTo(pathToDelete);
            path = pathToDelete;

            if (recursive)
            {
                var subfolders = Directory.EnumerateDirectories(path);
                foreach (var s in subfolders)
                {
                    if (excludes.Any(p => string.Equals(p, Path.GetDirectoryName(s), StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    DeleteDirectory(s, true, false, excludes);
                }
            }

            var files = Directory.EnumerateFiles(path);
            foreach (var f in files)
            {
                if (excludes.Any(p => string.Equals(p, Path.GetFileName(f), StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var fi = new FileInfo(f);
                fi.Attributes &= ~FileAttributes.ReadOnly;
                fi.Attributes &= ~FileAttributes.System;
                fi.Delete();
            }

            if (contentsOnly)
            {
                di.MoveTo(originalPath);
                di.Attributes = originalAttributes;
                return;
            }

            di.Attributes &= ~FileAttributes.ReadOnly;
            di.Attributes &= ~FileAttributes.System;
            di.Delete();
        }

        public static void CreateDirectoryAndSetAcl(string path, FileSystemAccessRule accessRule)
        {
            var destination = new DirectoryInfo(path);
            if (!destination.Exists)
            {
                destination.Create();
            }

            var accessRules = destination.GetAccessControl(AccessControlSections.Access);

            accessRules.ResetAccessRule(accessRule);
            destination.SetAccessControl(accessRules);
        }

        public static void UnzipToSubdirectory(string zipResourceName, string targetPath)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var zipStream = assembly.GetManifestResourceStream(zipResourceName);

            UnzipToSubdirectory(zipStream, targetPath);
        }

        internal static void UnzipToSubdirectory(Stream zipStream, string targetPath)
        {
            using var zip = ZipFile.Read(zipStream);
            zip.ExtractAll(targetPath, ExtractExistingFileAction.OverwriteSilently);
        }
    }
}