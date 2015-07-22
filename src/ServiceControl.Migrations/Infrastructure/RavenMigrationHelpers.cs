namespace ServiceControl.Migrations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text.RegularExpressions;

    public static class RavenMigrationHelpers
    {
        public static readonly string RavenMigrationsIdPrefix = "Migrations";

        public static string GetMigrationIdFromName(this Migration migration, char seperator = '/')
        {
            const char underscore = '_';
            var type = migration.GetType();
            var idSafeTypeName = Regex.Replace(type.Name, underscore + "{2,}", underscore.ToString())
                .Trim(underscore);
            var name = idSafeTypeName
                .Replace(underscore, seperator)
                .ToLowerInvariant();
            var executionOrder = type.GetMigrationAttribute().ExecutionOrder;

            return string.Join(seperator.ToString(), new[] {
                RavenMigrationsIdPrefix, name, executionOrder.ToString()
            }).ToLowerInvariant();
        }

        public static MigrationAttribute GetMigrationAttribute(this Type type)
        {
            var attribute = Attribute.GetCustomAttributes(type)
                .FirstOrDefault(x => x.GetType().IsAssignableFrom(typeof(MigrationAttribute)));
            return (MigrationAttribute)attribute;
        }

        public static IEnumerable<Type> GetLoadableTypes(this Assembly assembly)
        {
            if (assembly == null) throw new ArgumentNullException("assembly");
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null);
            }
        }
    }
}