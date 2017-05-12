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
                return false;
            }
            finally
            {
                AppDomain.Unload(tempDomain);
            }
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
