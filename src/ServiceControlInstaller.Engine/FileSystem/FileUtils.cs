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

        public static void UnzipToSubdirectory(string zipResourceName, string targetPath, string zipFolderNameToExtract)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var zipStream = assembly.GetManifestResourceStream(zipResourceName);

            UnzipToSubdirectory(zipStream, targetPath, zipFolderNameToExtract);
        }

        internal static void UnzipToSubdirectory(Stream zipStream, string targetPath, string zipFolderNameToExtract)
        {
            using var zip = ZipFile.Read(zipStream);
            var zipFilter = new ZipFilterEvaluator(zipFolderNameToExtract, targetPath);

            foreach (var e in zip)
            {
                var dir = Path.GetDirectoryName(e.FileName);

                if (zipFilter.Evaluate(e, out var filename))
                {
                    if (e.IsDirectory)
                    {
                        Directory.CreateDirectory(filename);
                        continue;
                    }

                    // Ensure folder exists
                    var folder = Path.GetDirectoryName(filename);
                    if (!Directory.Exists(folder))
                    {
                        Directory.CreateDirectory(folder);
                    }

                    using (var stream = new FileStream(filename, FileMode.OpenOrCreate))
                    {
                        e.Extract(stream);
                    }
                }
            }
        }


        class ZipFilterEvaluator
        {
            readonly string[] folderNameSegments;
            readonly string targetPath;

            static readonly char[] directorySplitChars = new[] { '/', '\\' };

            public ZipFilterEvaluator(string zipFolderNameToExtract, string targetPath)
            {
                folderNameSegments = zipFolderNameToExtract.Split(directorySplitChars, StringSplitOptions.RemoveEmptyEntries);
                this.targetPath = targetPath;
            }

            public bool Evaluate(ZipEntry zipEntry, out string resultPath)
            {
                var zipPathSegments = zipEntry.FileName.Split(directorySplitChars);
                resultPath = null;

                if (folderNameSegments.Length > 0)
                {
                    // If folder name is "a/b" and path is "a/file.txt" then the directory is too deep and the file can't match, so (2 + 1) > 2
                    // Also makes sure we don't get index out of range on next block
                    if (folderNameSegments.Length + 1 > zipPathSegments.Length)
                    {
                        return false;
                    }

                    for (var i = 0; i < folderNameSegments.Length; i++)
                    {
                        if (!string.Equals(zipPathSegments[i], folderNameSegments[i], StringComparison.OrdinalIgnoreCase))
                        {
                            return false;
                        }
                    }
                }

                // How many segments of the zip file name do wwe need? If just one, use Path.Combine(string, string)
                var nonPrefixZipFileSegmentsCount = zipPathSegments.Length - folderNameSegments.Length;
                if (nonPrefixZipFileSegmentsCount == 1)
                {
                    resultPath = Path.Combine(targetPath, zipPathSegments[folderNameSegments.Length]);
                    return true;
                }

                // For deeper paths, construct an array with the targetPath + the segments of the zip path after the desired folder name to Path.Combine(string[])
                var resultSegments = new string[nonPrefixZipFileSegmentsCount + 1];
                resultSegments[0] = targetPath;
                Array.Copy(zipPathSegments, folderNameSegments.Length, resultSegments, 1, nonPrefixZipFileSegmentsCount);
                resultPath = Path.Combine(resultSegments);
                return true;
            }
        }

        public static long GetDirectorySize(this DirectoryInfo dir)
        {
            return dir.GetFiles().Sum(file => file.Length) + dir.GetDirectories().Sum(d => d.GetDirectorySize());
        }
    }
}