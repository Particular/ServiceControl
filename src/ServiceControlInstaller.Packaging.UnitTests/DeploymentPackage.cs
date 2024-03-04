namespace Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using NUnit.Framework;

    public class DeploymentPackage
    {
        public DeploymentPackage(DirectoryInfo directory)
        {
            Directory = directory;
            ServiceName = directory.Name.Replace("Particular.", "");

            var instance = new DeploymentUnit(directory, "Instance", SearchOption.TopDirectoryOnly);

            foreach (var subFolder in directory.EnumerateDirectories().Where(d => d.Name is not "Persisters" and not "Transports"))
            {
                instance.Files.AddRange(subFolder.EnumerateFiles("*", SearchOption.AllDirectories));
            }

            DeploymentUnits = [instance];

            foreach (var componentCategoryDirectory in Directory.EnumerateDirectories().Where(d => d.Name is "Persisters" or "Transports"))
            {
                foreach (var componentDirectory in componentCategoryDirectory.EnumerateDirectories())
                {
                    DeploymentUnits.Add(new DeploymentUnit(componentDirectory, componentCategoryDirectory.Name));
                }
            }
        }

        public override string ToString() => ServiceName.Replace(".", " ");

        public string ServiceName { get; }

        public DirectoryInfo Directory { get; private set; }

        public List<DeploymentUnit> DeploymentUnits { get; protected set; } = [];

        public static IEnumerable<DeploymentPackage> All => GetDeployDirectory()
                .EnumerateDirectories()
                .Where(d => d.Name is not "PowerShellModules" and not "RavenDBServer")
                .Select(d => new DeploymentPackage(d));

        public static DirectoryInfo GetDeployDirectory()
        {
            var currentDirectory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);

            while (currentDirectory != null)
            {
                foreach (var folder in currentDirectory.EnumerateDirectories("deploy", SearchOption.TopDirectoryOnly))
                {
                    return folder;
                }

                currentDirectory = currentDirectory.Parent;
            }

            throw new Exception("Cannot find `deploy` folder");
        }

        public class DeploymentUnit
        {
            public DeploymentUnit(DirectoryInfo directory, string category, SearchOption searchOption = SearchOption.AllDirectories)
            {
                Directory = directory;
                Files = directory.EnumerateFiles("*", searchOption).ToList();
                Category = category;
                Name = directory.Name;
                FullName = $"{Category}/{Name}";
            }

            public DirectoryInfo Directory { get; }

            public List<FileInfo> Files { get; }

            public string Name { get; }

            public string FullName { get; }

            public string Category { get; }
        }
    }
}