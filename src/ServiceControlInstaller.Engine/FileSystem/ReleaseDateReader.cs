using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace ServiceControlInstaller.Engine.FileSystem
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;

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
                    var myAttributeData = assembly.GetCustomAttributesData().SingleOrDefault(ca => ca.AttributeType.Name == "ReleaseDateAttribute");

                    var dateString = (string)myAttributeData?.ConstructorArguments[0].Value;
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
