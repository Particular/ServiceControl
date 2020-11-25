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
            var tempDomain = AppDomain.CreateDomain("TemporaryAppDomain");
            try
            {
                var loaderType = typeof(AssemblyReleaseDateReader);
                var loader = (AssemblyReleaseDateReader)tempDomain.CreateInstanceFrom(Assembly.GetExecutingAssembly().Location, loaderType.FullName).Unwrap();
                releaseDate = loader.GetReleaseDate(exe);
                return true;
            }
            catch
            {
                try
                {
                    return TryReadReleaseDateAttributeUsingMonoCecil(exe, out releaseDate);
                }
                catch
                {
                    return false;
                }
            }
            finally
            {
                AppDomain.Unload(tempDomain);
            }
        }

        static bool TryReadReleaseDateAttributeUsingMonoCecil(string exe, out DateTime releaseDate)
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

            releaseDate = DateTime.MinValue;
            return false;
        }

        class AssemblyReleaseDateReader : MarshalByRefObject
        {
            internal DateTime GetReleaseDate(string assemblyPath)
            {
                try
                {
                    var assembly = Assembly.ReflectionOnlyLoadFrom(assemblyPath);
                    var releaseDateAttribute = assembly.GetCustomAttributesData().FirstOrDefault(p => p.Constructor?.ReflectedType?.Name == "ReleaseDateAttribute");
                    var x = (string)releaseDateAttribute?.ConstructorArguments[0].Value;
                    return DateTime.ParseExact(x, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                }
                catch (Exception)
                {
                    return DateTime.MinValue;
                }
            }
        }
    }
}