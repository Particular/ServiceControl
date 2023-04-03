namespace ServiceControl.Architecture.UnitTests;

using System.Text.RegularExpressions;

class UseNamespaceComponentMappingStrategy : IComponentMappingStrategy
{
    public string? FindComponent(string path) =>
        (from line in File.ReadLines(path)
         let match = Regex.Match(line, @"namespace\s+([^\s{;]+)")
         where match.Success
         select match.Result("$1")
        ).FirstOrDefault();
}