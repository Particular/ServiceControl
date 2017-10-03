namespace Particular.Licensing
{
    using System;
    using System.Linq;
    using System.Reflection;

    static class ReleaseDateReader
    {
        public static DateTime GetReleaseDate()
        {
            var attribute = Assembly.GetExecutingAssembly()
                .GetCustomAttributes(false)
                .FirstOrDefault(x => x is ReleaseDateAttribute) as ReleaseDateAttribute;

            if (attribute == null)
            {
                throw new Exception("No ReleaseDateAttribute could be found in assembly, please make sure GitVersion is enabled");
            }

            return UniversalDateParser.Parse(attribute.OriginalDate);
        }
    }
}