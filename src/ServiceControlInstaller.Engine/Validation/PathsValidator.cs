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
            public string Path { get; set; }
        }

        internal List<IContainInstancePaths> Instances;
        List<PathInfo> paths;

        internal PathsValidator(IContainInstancePaths instance)
        {
            var pathList = new List<PathInfo>
            {
                new PathInfo{ Name = "log path", Path = Environment.ExpandEnvironmentVariables(instance.LogPath ?? String.Empty)},
                new PathInfo{ Name = "DB path", Path = Environment.ExpandEnvironmentVariables(instance.DBPath ?? String.Empty)},
                new PathInfo{ Name = "install path", Path = Environment.ExpandEnvironmentVariables(instance.InstallPath ?? String.Empty)},
            };
            paths = pathList.Where(p => !string.IsNullOrWhiteSpace(p.Path)).ToList();
        }

        void RunValidation(bool includeNewInstanceChecks)
        {
            try
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
            catch (EngineValidationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new EngineValidationException("An unhandled exception occured while trying to validate the paths.", ex);
            }
        }

        void CheckPathsAreEmpty()
        {
            foreach (var pathInfo in paths)
            {
                var directory = new DirectoryInfo(pathInfo.Path);
                if (directory.Exists)
                {
                    var flagFile = Path.Combine(directory.FullName, ".notconfigured");
                    if (File.Exists(flagFile))
                        continue;  // flagfile will be present if we've unpacked and had a config failure.  In this case it's OK for the directory to have content
                    if (directory.GetFileSystemInfos().Any())
                        throw new EngineValidationException($"The directory specified as the {pathInfo.Name} is not empty.");
                }
            }
        }

        public static void Validate(ServiceControlInstanceMetadata instance)
        {
            var validator = new PathsValidator(instance)
            {
                Instances = ServiceControlInstance.Instances().AsEnumerable<IContainInstancePaths>().ToList()
            };
            validator.RunValidation(true);
        }

        public static void Validate(ServiceControlInstance instance)
        {
            var validator = new PathsValidator(instance)
            {
                Instances = ServiceControlInstance.Instances().Where(p => p.Name != instance.Name).AsEnumerable<IContainInstancePaths>().ToList()
            };
            validator.RunValidation(false);
        }

        internal void CheckPathsNotUsedInOtherInstances()
        {
            var existingInstancePaths = new List<string>();
            existingInstancePaths.AddRange(Instances.Where(q => !string.IsNullOrWhiteSpace(q.InstallPath)).Select(p => Environment.ExpandEnvironmentVariables(p.InstallPath)));
            existingInstancePaths.AddRange(Instances.Where(q => !string.IsNullOrWhiteSpace(q.DBPath)).Select(p => Environment.ExpandEnvironmentVariables(p.DBPath)));
            existingInstancePaths.AddRange(Instances.Where(q => !string.IsNullOrWhiteSpace(q.LogPath)).Select(p => Environment.ExpandEnvironmentVariables(p.LogPath)));
            existingInstancePaths = existingInstancePaths.Distinct().ToList();
            foreach (var path in paths.Where(path => existingInstancePaths.Contains(path.Path, StringComparer.OrdinalIgnoreCase)))
            {
                throw new EngineValidationException($"The {path.Name} specified is already assigned to another instance");
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
                        throw new EngineValidationException($"Nested paths are not supported. The {possibleChild.Name} is nested under {path.Name}");
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
                    throw new EngineValidationException($"The {path.Name} is set to an invalid path");
                }

                if (uri.IsUnc)
                {
                    throw new EngineValidationException($"The {path.Name} is invalid,  UNC paths are not supported.");
                }

                if (uri.Scheme != Uri.UriSchemeFile)
                {
                    throw new EngineValidationException($"The {path.Name} is set to an invalid path");
                }

                if (!Path.IsPathRooted(uri.LocalPath))
                {
                    throw new EngineValidationException($"A full path is required for {path.Name}");
                }

                var pathDriveLetter = Path.GetPathRoot(uri.LocalPath)[0].ToString();
                if (!driveletters.Contains(pathDriveLetter, StringComparer.OrdinalIgnoreCase))
                {
                    throw new EngineValidationException($"The {path.Name} does not go to a supported drive");
                }
            }
        }
    }
}