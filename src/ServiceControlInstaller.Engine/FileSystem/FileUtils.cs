namespace ServiceControlInstaller.Engine.FileSystem
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Security.AccessControl;
    using Ionic.Zip;

    internal class FileUtils
    {
        public static string SanitizeFolderName(string folderName)
        {
            return Path.GetInvalidPathChars().Aggregate(folderName, (current, c) => current.Replace(c, '-'));
        }

        public static void DeleteDirectory(string path, bool recursive, bool contentsOnly, params string[] excludes)
        {
           if (!Directory.Exists(path))
           {
               return;
           } 

           if (recursive)
           {
                var subfolders = Directory.GetDirectories(path);
                foreach (var s in subfolders)
                {
                    if (excludes.Any(p => string.Equals(p, Path.GetDirectoryName(s), StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }
                    DeleteDirectory(s, true, false, excludes);
                }
            }

            var files = Directory.GetFiles(path);
            foreach (var f in files)
            {
                if (excludes.Any(p => string.Equals(p, Path.GetDirectoryName(f), StringComparison.OrdinalIgnoreCase)))
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
                return;
            }
            var di = new DirectoryInfo(path);
            if (di.Exists)
            {
                di.Attributes &= ~FileAttributes.ReadOnly;
                di.Attributes &= ~FileAttributes.System;
                di.Delete();
            }
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

        public static void UnzipToSubdirectory(string zipFilePath, string targetPath, string zipFolderNameToExtract)
        {
            using (var zip = ZipFile.Read(zipFilePath))
            {
                foreach (var e in zip)
                {
                    var dir = Path.GetDirectoryName(e.FileName);
                    string filename = null;
                    
                    // ReSharper disable once PossibleNullReferenceException
                    if (dir.StartsWith(zipFolderNameToExtract, StringComparison.OrdinalIgnoreCase))
                    {
                        filename = Path.Combine(targetPath, e.FileName.Substring(zipFolderNameToExtract.Length + 1));
                    }

                    if (filename != null)
                    {
                        if (e.IsDirectory)
                        {
                            Directory.CreateDirectory(filename);
                            continue;
                        }
                        using (var stream = new FileStream(filename, FileMode.OpenOrCreate))
                        {
                            e.Extract(stream);
                        }
                    }
                }
            }

        }
    }
}