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
            try
            {
                using (var assemblyDefinition = Mono.Cecil.AssemblyDefinition.ReadAssembly(exe))
                {
                    var customAttribute = assemblyDefinition.CustomAttributes.SingleOrDefault(ca => ca.AttributeType.Name == "ReleaseDateAttribute");
                    var constructorArgument = customAttribute?.ConstructorArguments[0];
                    var dateString = (string)constructorArgument?.Value;
                    if (!string.IsNullOrWhiteSpace(dateString))
                    {
                        releaseDate = DateTime.ParseExact(dateString, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                        return true;
                    }
                }
            }
            catch
            {
                //NOP
            }

            releaseDate = DateTime.MinValue;
            return false;
        }
    }
}