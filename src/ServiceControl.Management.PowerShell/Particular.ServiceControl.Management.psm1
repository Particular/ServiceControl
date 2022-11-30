<#
    .Synopsis
    Lists the commands and aliases provided by the Particular.ServiceControl.Management module.

    .Description
    Lists the commands and aliases provided by the Particular.ServiceControl.Management module. This command is meant as a starting point as it lists what is available in the module.
#>
Function Get-ServiceControlManagementCommands {
    PROCESS {
        Get-Alias -Scope Global | Where-Object ModuleName -EQ "Particular.ServiceControl.Management" | Select-Object -Property @{Name = 'Alias'; Expression = {$_.Name}}, @{Name = 'Command'; Expression = {$_.Definition}}
    }
}

New-Alias -Value Get-ServiceControlInstances -Name sc-instances
New-Alias -Value Invoke-ServiceControlInstanceUpgrade -Name  sc-upgrade
New-Alias -Value Remove-ServiceControlInstance -Name  sc-delete
New-Alias -Value New-ServiceControlInstance -Name  sc-add
New-Alias -Value Get-MonitoringInstances -Name mon-instances
New-Alias -Value Invoke-MonitoringInstanceUpgrade -Name  mon-upgrade
New-Alias -Value Remove-MonitoringInstance -Name  mon-delete
New-Alias -Value New-MonitoringInstance -Name  mon-add
New-Alias -Value Get-ServiceControlAuditInstances -Name audit-instances
New-Alias -Value Invoke-ServiceControlAuditInstanceUpgrade -Name  audit-upgrade
New-Alias -Value Remove-ServiceControlAuditInstance -Name  audit-delete
New-Alias -Value New-ServiceControlAuditInstance -Name  audit-add
New-Alias -Value Get-ServiceControlLicense -Name  sc-findlicense
New-Alias -Value Import-ServiceControlLicense -Name  sc-addlicense
New-Alias -Value Test-IfPortIsAvailable -Name  port-check
New-Alias -Value Get-SecurityIdentifier -Name  user-sid
New-Alias -Value Get-UrlAcls -Name  urlacl-list
New-Alias -Value Add-UrlAcl -Name  urlacl-add
New-Alias -Value Remove-UrlAcl -Name  urlacl-delete
New-Alias -Value Get-ServiceControlTransportTypes -Name  sc-transportsinfo
New-Alias -Value Get-ServiceControlManagementCommands -Name  sc-help
New-Alias -Value Get-ServiceControlRemotes -Name sc-remotes
New-Alias -Value Add-ServiceControlRemote -Name sc-addremote
New-Alias -Value Remove-ServiceControlRemote -Name sc-deleteremote

Export-ModuleMember * -Alias *

Write-Verbose "Use sc-help to list ServiceControl management commands" -Verbose
