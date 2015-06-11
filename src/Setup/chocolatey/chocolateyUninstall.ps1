$psFile = Join-Path "$(Split-Path -parent $MyInvocation.MyCommand.Definition)" 'elevatedUninstall.ps1'
Start-ChocolateyProcessAsAdmin "& `'$psFile`'"