@{
	GUID = 'ABFF92C4-A8BA-CAAA-A40D-8C97F3B28934'
	Author = 'Particular Software'
	Description = 'ServiceControl Management'
	ModuleVersion = '{{Version}}'
	CLRVersion = '4.0'
	DotNetFrameworkVersion = '4.0'
	NestedModules = @('ServiceControlInstaller.PowerShell.DLL',  'ServiceControlMgmt.psm1')
    PowerShellVersion = '3.0'
	CompanyName = 'Particular Software'
	Copyright = 'Particular Software, Copyright © 2015'
	FunctionsToExport='*'
	CmdletsToExport = '*'
	VariablesToExport = '*'
	AliasesToExport = '*'
	FormatsToProcess = @()
    RequiredAssemblies = @('ServiceControlInstaller.Engine.DLL')
}