using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ionic.Zip;
using Microsoft.Build.Framework;

namespace DeploymentZipTask
{
    public class AddToZip : ITask
    {
        [Required]
        public string ZipFileName { get; set; }

        [Required]
        public string SourceFolder { get; set; }

        [Required]
        public string IncludeMask { get; set; }

        public string ExcludeMask { get; set; }

        public string ZipFolder { get; set; }

        public bool ForceOverwrite { get; set; }

        public bool Execute()
        {

            var zipFilePath = Path.GetFullPath(ZipFileName);
            var sourceFolder = Path.GetFullPath(SourceFolder);

            var appendMode = (File.Exists(zipFilePath) & !ForceOverwrite);

            this.LogInfo("ZipFile {0} : {1} ",
                    appendMode ? "Open" : "Create",
                    zipFilePath);
            this.LogInfo("SourceFolder : {0}", sourceFolder);
            this.LogInfo("IncludeMask : {0}", IncludeMask);
            this.LogInfo("ExcludeMask : {0}", ExcludeMask);
            this.LogInfo("TargetFolderInZipMask : {0}", ZipFolder);

            using (var zipFile = (appendMode) ? ZipFile.Read(zipFilePath) : new ZipFile(zipFilePath))
            {
                var files = GetFiles(sourceFolder);

                if (files.Count > 0)
                {
                    zipFile.AddFiles(files, ZipFolder ?? "");
                }
                else
                {
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(ZipFolder))
                        {
                            zipFile.AddDirectoryByName(ZipFolder);
                        }
                    }
                    catch (Exception)
                    {
                         //Ignore - Directory Exists
                    }
                }
                zipFile.Save(); 
            }
            
            return true;
        }

        private List<string> GetFiles(string sourceFolder)
        {
            var files = new DirectoryInfo(sourceFolder).GetFiles(IncludeMask);
            var fileList = (string.IsNullOrWhiteSpace(ExcludeMask) 
                        ? files 
                        : files.Where(p => !PatternMatcher.StrictMatchPattern(ExcludeMask, p.Name)))
                    .Select(p => p.FullName).ToList();
            this.LogInfo("'{0}' returned {1} files that matched the given file masks", sourceFolder, fileList.Count);

            return fileList;
        }


        public IBuildEngine BuildEngine { get; set; }
        public ITaskHost HostObject { get; set; }
    }
}
