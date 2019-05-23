namespace DeploymentZipTask
{
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using Microsoft.Build.Framework;
    using Microsoft.Build.Utilities;

    public class AddToZip : Task
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

        public override bool Execute()
        {
            var zipFilePath = Path.GetFullPath(ZipFileName);
            var sourceFolder = Path.GetFullPath(SourceFolder);

            var appendMode = (File.Exists(zipFilePath) & !ForceOverwrite);

            Log.LogMessage("ZipFile {0} : {1} ",
                appendMode ? "Open" : "Create",
                zipFilePath);
            Log.LogMessage("SourceFolder : {0}", sourceFolder);
            Log.LogMessage("IncludeMask : {0}", IncludeMask);
            Log.LogMessage("ExcludeMask : {0}", ExcludeMask);
            Log.LogMessage("TargetFolderInZipMask : {0}", ZipFolder);

            using (var archive = ZipFile.Open(zipFilePath, ZipArchiveMode.Update))
            {
                foreach (var file in GetFiles(sourceFolder))
                {
                    var entryName = Path.Combine(ZipFolder, Path.GetFileName(file)).Replace('\\', '/');
                    var entry = archive.GetEntry(entryName);
                    entry?.Delete();
                    archive.CreateEntryFromFile(file, entryName);
                }
            }

            return true;
        }

        private List<string> GetFiles(string sourceFolder)
        {
            var files = new DirectoryInfo(sourceFolder).GetFiles();

            var includeMasks = SafeSplit(IncludeMask);
            var excludeMasks = SafeSplit(ExcludeMask);

            var fileList = files.Where(
                    x => includeMasks.Any(m => PatternMatcher.StrictMatchPattern(m, x.Name))
                      && excludeMasks.Any(m => PatternMatcher.StrictMatchPattern(m, x.Name)) == false)
                .Select(x => x.FullName)
                .ToList();

            Log.LogMessage("'{0}' returned {1} files that matched the given file masks", sourceFolder, fileList.Count);

            return fileList;
        }

        private string[] SafeSplit(string s) => (s ?? string.Empty).Split(';').Select(x => x.Trim()).ToArray();
    }
}
