namespace ServiceControlInstaller.Engine.FileSystem
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
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
                    var attributeData = assembly.GetCustomAttributesData().SingleOrDefault(ca => ca.AttributeType.Name == "ReleaseDateAttribute");

                    var dateString = (string)attributeData?.ConstructorArguments[0].Value;
                    releaseDate = DateTime.ParseExact(dateString, "yyyy-MM-dd", CultureInfo.InvariantCulture);

                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
