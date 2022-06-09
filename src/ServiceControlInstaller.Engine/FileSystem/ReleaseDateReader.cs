namespace ServiceControlInstaller.Engine.FileSystem
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Runtime.InteropServices;

    public class ReleaseDateReader
    {
        public static bool TryReadReleaseDateAttribute(string exe, out DateTime releaseDate)
        {
            releaseDate = DateTime.MinValue;

            try
            {
                var runtimeAssemblies = Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll");

                var allAssemblies = new List<string>();
                allAssemblies.AddRange(runtimeAssemblies);
                allAssemblies.Add(exe);

                var resolver = new PathAssemblyResolver(allAssemblies.ToArray());
                var mlc = new MetadataLoadContext(resolver);

                using (mlc)
                {
                    var assembly = mlc.LoadFromAssemblyPath(exe);

                    // See ReleaseDateReader implementation of the licensing sources package
                    var attributes = assembly.GetCustomAttributesData();

                    string currentMinorCommitDate = null;
                    string commitDate = null;

                    foreach (var attribute in attributes)
                    {
                        if (attribute.AttributeType.Name == "AssemblyMetadataAttribute" && attribute.ConstructorArguments.Count == 2)
                        {
                            if ((attribute.ConstructorArguments[0].Value as string) == "CurrentMinorCommitDate")
                            {
                                currentMinorCommitDate = (string)attribute.ConstructorArguments[1].Value;
                            }
                            else if ((attribute.ConstructorArguments[0].Value as string) == "CommitDate")
                            {
                                commitDate = (string)attribute.ConstructorArguments[1].Value;
                            }
                        }
                    }

                    var commitDateString = currentMinorCommitDate ?? commitDate;

                    if (DateTimeOffset.TryParse(commitDateString, out var releaseDateOffset))
                    {
                        releaseDate = releaseDateOffset.UtcDateTime;
                        return true;
                    }

                    return false;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
