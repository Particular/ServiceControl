namespace ServiceControlInstaller.Engine.Validation
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using ServiceControlInstaller.Engine.Instances;

    internal class PathsValidator
    {
        class PathInfo
        {
            public string Name { get; set; }
            public string Path{ get; set; }
        }

        internal List<IContainInstancePaths> instances;
        List<PathInfo> paths;

        internal PathsValidator(IContainInstancePaths instance)
        {
            var pathList = new List<PathInfo>
            {
                new PathInfo{ Name = "log path", Path = Environment.ExpandEnvironmentVariables(instance.LogPath ?? "")},
                new PathInfo{ Name = "DB path", Path = Environment.ExpandEnvironmentVariables(instance.DBPath ?? "")},
                new PathInfo{ Name = "install path", Path = Environment.ExpandEnvironmentVariables(instance.InstallPath ?? "")},
            };
            paths = pathList.Where(p => !string.IsNullOrWhiteSpace(p.Path)).ToList();
        }

        void RunValidation(bool includeNewInstanceChecks)
        {
            CheckPathsAreValid();
            CheckNoNestedPaths();
            CheckPathsAreUnique();
            CheckPathsNotUsedInOtherInstances();

            //Do Checks that only make sense on add instance 
            if (includeNewInstanceChecks)
            {
                CheckPathsAreEmpty();
            }
        }

        void CheckPathsAreEmpty()
        {
            foreach (var pathInfo in paths)
            {
                var directory = new DirectoryInfo(pathInfo.Path);
                if (directory.Exists && directory.GetFileSystemInfos().Any())
                {
                    throw  new EngineValidationException(string.Format("The directory specified as the {0} is not empty.", pathInfo.Name));
                }
            }
        }

        public static void Validate(ServiceControlInstanceMetadata instance)
        {
            var validator = new PathsValidator(instance)
            {
                instances = ServiceControlInstance.Instances().AsEnumerable<IContainInstancePaths>().ToList()
            };
            validator.RunValidation(true);
        }

        public static void Validate(ServiceControlInstance instance)
        {
            var validator = new PathsValidator(instance)
            {
                instances = ServiceControlInstance.Instances().Where(p => p.Name != instance.Name).AsEnumerable<IContainInstancePaths>().ToList()
            };
            validator.RunValidation(false);
        }

        internal void CheckPathsNotUsedInOtherInstances()
        {
            var existingInstancePaths = new List<string>();
            existingInstancePaths.AddRange(instances.Where(q => !string.IsNullOrWhiteSpace(q.InstallPath)).Select(p => Environment.ExpandEnvironmentVariables(p.InstallPath)));
            existingInstancePaths.AddRange(instances.Where(q => !string.IsNullOrWhiteSpace(q.DBPath)).Select(p => Environment.ExpandEnvironmentVariables(p.DBPath)));
            existingInstancePaths.AddRange(instances.Where(q => !string.IsNullOrWhiteSpace(q.LogPath)).Select(p => Environment.ExpandEnvironmentVariables(p.LogPath)));
            existingInstancePaths = existingInstancePaths.Distinct().ToList();
            foreach (var path in paths.Where(path => existingInstancePaths.Contains(path.Path, StringComparer.OrdinalIgnoreCase)))
            {
                throw new EngineValidationException(string.Format("The {0} specified is already assigned to another instance", path.Name));
            }
        }

        internal void CheckNoNestedPaths()
        {
            foreach (var path in paths)
            {
                foreach (var possibleChild in paths)
                {
                    if (path.Name == possibleChild.Name)
                        continue;
                    if (possibleChild.Path.IndexOf(path.Path, StringComparison.OrdinalIgnoreCase) > -1)
                    {
                        throw new EngineValidationException(string.Format("Nested paths are not supported. The {0} is nested under {1}", possibleChild.Name, path.Name));
                    }
                }
            }
        }
        
        internal void CheckPathsAreUnique()
        {
            if (paths.Select(p => p.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != paths.Count)
            {
                throw new EngineValidationException("The installation path, log path and database path must be unique");
            }
        }
    
        internal void CheckPathsAreValid()
        {
            var driveletters = DriveInfo.GetDrives().Where(p => p.DriveType != DriveType.Network && p.DriveType != DriveType.CDRom)
                .Select(p => p.Name[0].ToString())
                .ToArray();

            foreach (var path in paths)
            {
                Uri uri;

                if (!Uri.TryCreate(path.Path, UriKind.Absolute, out uri))
                {
                    throw new EngineValidationException(string.Format("The {0} is set to an invalid path", path.Name));
                }

                if (uri.IsUnc)
                {
                    throw new EngineValidationException(string.Format("The {0} is invalid,  UNC paths are not supported.", path.Name));
                }

                if (uri.Scheme != Uri.UriSchemeFile)
                {
                    throw new EngineValidationException(string.Format("The {0} is set to an invalid path", path.Name));
                }

                if (!Path.IsPathRooted(uri.LocalPath))
                {
                    throw new EngineValidationException(string.Format("A full path is required for {0}", path.Name));
                }

                var pathDriveLetter = Path.GetPathRoot(uri.LocalPath)[0].ToString();
                if (!driveletters.Contains(pathDriveLetter, StringComparer.OrdinalIgnoreCase))
                {
                    throw new EngineValidationException(string.Format("The {0} does not go to a supported drive", path.Name));
                }
            }
        }
    }
}
