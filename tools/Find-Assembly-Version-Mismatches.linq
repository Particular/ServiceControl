<Query Kind="Statements" />

// After a debug build, find all assemblies where we have different versions

var root = Path.Combine(Path.GetDirectoryName(Util.CurrentQueryPath), "..", "src");

// NOTE: These are not .NET assemblies
var ignoredAssemblies = new[]
{
	"Microsoft.Data.SqlClient.SNI.x64",
	"Microsoft.Data.SqlClient.SNI.x86"
};

(
from path in Directory.EnumerateFiles(root, "*.dll", SearchOption.AllDirectories)
where path.Contains(@"bin\Debug")
let assembly = Path.GetFileNameWithoutExtension(path)
where ignoredAssemblies.Contains(assembly) == false
group path by assembly into byAssemblyName
let referenceCount = byAssemblyName.Count()
where referenceCount > 1
let versions =  
				(from p in byAssemblyName
			   let assemblyName = AssemblyName.GetAssemblyName(p)
			   let projectName = p.Substring(root.Length +1).Split(Path.DirectorySeparatorChar).First()
			   orderby projectName
			   group projectName by assemblyName.Version.ToString() into byVersion
			   let projectCount = byVersion.Count()
			   orderby projectCount descending
			   select new {
			   	Version = byVersion.Key,
				   Projects = Util.OnDemand($"{projectCount}", () => byVersion.ToArray())
			   }).ToArray()
where versions.Count() > 1
orderby versions.Count(), referenceCount descending
select new 
{
	Assembly = byAssemblyName.Key,
	ProjectReferences = referenceCount,
	Versions = versions
}
).Dump();