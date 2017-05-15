<# 
    .Synopsis
    Lists the commands and aliases provided by the ServiceControlMgmt Module.
    
	.Description
	Lists the commands and aliases provided by the ServiceControlMgmt Module.  This command is ment
	as a starting point as it list what is available in the module.
#>
Function Get-ServiceControlMgmtCommands {
    PROCESS {
        Get-Alias -Scope Global | ? ModuleName -EQ "ServiceControlMgmt" | Select-Object -Property @{Name = 'Alias'; Expression = {$_.Name}}, @{Name = 'Command'; Expression = {$_.Definition}}
    }
}

<# Init Code #>
$requiredDLL = [AppDomain]::CurrentDomain.GetAssemblies() | Select -ExpandProperty Modules | ? Name -eq ServiceControlInstaller.Engine.dll 
if (!$requiredDLL) {
    throw "This module was imported incorrectly - use 'Import-Module .\ServiceControlMgmt.psd1' to load this module"  
}

New-Alias -Value Get-ServiceControlInstances -Name sc-instances 
New-Alias -Value Invoke-ServiceControlInstanceUpgrade -Name  sc-upgrade 
New-Alias -Value Remove-ServiceControlInstance -Name  sc-delete 
New-Alias -Value New-ServiceControlInstance -Name  sc-add 
New-Alias -Value Get-MonitoringInstances -Name mon-instances 
New-Alias -Value Invoke-MonitoringinstanceUpgrade -Name  mon-upgrade 
New-Alias -Value Remove-MonitoringInstance -Name  mon-delete 
New-Alias -Value New-MonitoringInstance -Name  mon-add 
New-Alias -Value Get-ServiceControlLicense -Name  sc-findlicense 
New-Alias -Value Import-ServiceControlLicense -Name  sc-addlicense 
New-Alias -Value Test-IfPortIsAvailable -Name  port-check
New-Alias -Value Get-SecurityIdentifier -Name  user-sid
New-Alias -Value Get-UrlAcls -Name  urlacl-list 
New-Alias -Value Add-UrlAcl -Name  urlacl-add 
New-Alias -Value Remove-UrlAcl -Name  urlacl-delete
New-Alias -Value Get-ServiceControlTransportTypes -Name  sc-transportsinfo
New-Alias -Value Get-ServiceControlMgmtCommands -Name  sc-help

Export-ModuleMember * -Alias * 

Write-Verbose "Use SC-help to list ServiceControl Management commands" -Verbose

 
