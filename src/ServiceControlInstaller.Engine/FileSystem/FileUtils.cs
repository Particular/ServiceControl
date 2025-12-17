namespace ServiceControlInstaller.Engine.FileSystem
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Reflection;
    using System.Security.AccessControl;
    using System.Threading;

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

            // ADD randomness to path, not replacing folder name so that it can still be identified
            string pathToDelete = path + Path.GetRandomFileName();

            try
            {
                // Move folder to ensure no files are in use so that it is less likely that we corrupt the folder
                // when still in use
                RunWithRetries(() => di.MoveTo(pathToDelete));
                path = pathToDelete;
                DeleteDirectoryInternal(path, recursive, contentsOnly, excludes);

                if (contentsOnly)
                {
                    di.MoveTo(originalPath);
                }
            }
            catch (Exception ex)
            {
                throw new($"Failure during folder {(contentsOnly ? "cleanup" : "removal")}. Remaining folder content is available at: {path}", ex);
            }
            // Intentionally not moving back in a finally as folder is likely corrupted
        }

        static void DeleteDirectoryInternal(string path, bool recursive, bool contentsOnly, params string[] excludes)
        {
            var di = new DirectoryInfo(path);

            if (!di.Exists)
            {
                return;
            }

            if (recursive)
            {
                var subfolders = Directory.EnumerateDirectories(path);
                foreach (var s in subfolders)
                {
                    if (excludes.Any(p => string.Equals(p, Path.GetDirectoryName(s), StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    DeleteDirectoryInternal(s, true, false, excludes);
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

                RunWithRetries(() =>
                {
                    fi.Attributes &= ~(FileAttributes.ReadOnly | FileAttributes.System);
                    fi.Delete();
                });
            }

            if (contentsOnly)
            {
                return;
            }

            RunWithRetries(() =>
            {
                di.Attributes &= ~(FileAttributes.ReadOnly | FileAttributes.System);
                di.Delete();
            });
        }

        public static void CloneDirectory(string srcDir, string destDir, params string[] includes)
        {
            Directory.CreateDirectory(destDir);
            var files = Directory.EnumerateFiles(srcDir, "*", SearchOption.AllDirectories);

            foreach (var srcFile in files)
            {
                var filename = Path.GetFileName(srcFile);
                var isMatch = includes.Any(p => string.Equals(p, filename, StringComparison.OrdinalIgnoreCase));

                if (isMatch)
                {
                    var relativePath = srcFile.Substring(srcDir.Length + 1);
                    var destFile = Path.Combine(destDir, relativePath);
                    var destFileDir = Path.GetDirectoryName(destFile);
                    Directory.CreateDirectory(destFileDir);
                    File.Copy(srcFile, destFile);
                }
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

        public static void UnzipToSubdirectory(string zipResourceName, string targetPath)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var zipStream = assembly.GetManifestResourceStream(zipResourceName);

            UnzipToSubdirectory(zipStream, targetPath);
        }

        internal static void UnzipToSubdirectory(Stream zipStream, string targetPath)
        {
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true, entryNameEncoding: null);
            archive.ExtractToDirectory(targetPath, overwriteFiles: true);

            // Validate 3rd-party security software didn't delete any of the files, but first, a small delay
            // so that any tool out there has a chance to remove the file before prematurely declaring victory
            Thread.Sleep(1000);
            foreach (var entry in archive.Entries)
            {
                var pathParts = entry.FullName.Split('/', '\\');
                var allParts = new string[pathParts.Length + 1];
                allParts[0] = targetPath;
                Array.Copy(pathParts, 0, allParts, 1, pathParts.Length);
                var destinationPath = Path.Combine(allParts);
                var fileInfo = new FileInfo(destinationPath);
                if (!fileInfo.Exists || fileInfo.Length != entry.Length)
                {
                    throw new Exception($"The following file was removed after install, perhaps due to a false positive in a 3rd-party security tool. Add an exception for the path in the tool's configuration and try again: " + destinationPath);
                }
            }
        }

        static void RunWithRetries(Action action)
        {
            var attempts = 10;
            while (true)
            {
                try
                {
                    action();
                    break;
                }
                catch (IOException ex) when (--attempts > 0)
                {
                    Debug.WriteLine($"ServiceControlInstaller.Engine.FileSystem.FileUtils::RunWithRetries Action failed, {attempts} attempts remaining. Reason: {ex.Message} ({ex.GetType().FullName})");
                    // Yes, Task.Delay would be better but would require all calls to be async
                    // and in 99.9% this sleep will not hit
                    Thread.Sleep(100);
                }
            }
        }
    }
}