$tempFile = [System.IO.Path]::GetTempFileName()
$psFile = Join-Path "$(Split-Path -parent $MyInvocation.MyCommand.Definition)" 'elevatedUninstall.ps1'
try
{
	Start-ChocolateyProcessAsAdmin "& `'$psFile`' `'$tempFile`'"
	Get-Content $tempFile
}
catch
{
	Get-Content $tempFile
	throw $_
}
finally
{
	If (Test-Path $tempFile){
		Remove-Item $tempFile
	}
}
	