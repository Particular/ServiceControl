try {

    Start-Transcript -Path $args[0] -Force

	$app = Get-WmiObject -Class Win32_Product | Where-Object { $_.Name -like "ServiceControl*"  -and ($_.Version -eq "MajorMinorPatch") }
	$result = $app.Uninstall();

}
catch {
    $_ | Out-Default
	throw $_  
}
finally {
    Stop-Transcript
}