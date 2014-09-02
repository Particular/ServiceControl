$productName = "ServiceControl";
$version = "{{ReleaseName}}";

$app = Get-WmiObject -Class Win32_Product | Where-Object { $_.Name -like "*$productName*"  -and ($_.Version -eq "$version") }
if($app -eq $null) 
{
	Write-Warning "Could not find an installed program for $productName matching version $version to uninstall. It may have been manualy removed. To check the cuurent status ensure that no instance of $productName exists in 'Programs and Features'."
}
else
{
	Write "Attempting to uninstall $productName $version from Programs."
	$result = $app.Uninstall();
	$returnValue = [int]$result.ReturnValue
	if ($returnValue -ne 0)
	{
		Write-Error "Could not uninstall $productName $version. The return code was $returnValue. For information on error codes see http://msdn.microsoft.com/en-us/library/aa372835 http://msdn.microsoft.com/en-us/library/aa376931."
	}
}