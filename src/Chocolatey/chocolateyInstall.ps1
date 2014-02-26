
$packageName = "ServiceControl"
$url = "https://github.com/Particular/ServiceControl/releases/download/SemVer/Particular.ServiceControl-SemVer.exe"

try {

    $chocTempDir = Join-Path $env:TEMP "chocolatey"
    $tempDir = Join-Path $chocTempDir "$packageName"
    if (![System.IO.Directory]::Exists($tempDir)) {[System.IO.Directory]::CreateDirectory($tempDir) | Out-Null}
    $file = Join-Path $tempDir "$($packageName)Install.exe"
    $logFile = Join-Path $tempDir "msiexe.log"

	If (Test-Path $logFile){
		Remove-Item $logFile
	}

    Get-ChocolateyWebFile $packageName $file $url 
	$msiArguments  ="/quiet  /L*V `"$logFile`""
	Write-Host "Starting installer with arguments: $msiArguments";
    Start-ChocolateyProcessAsAdmin "$msiArguments" $file -validExitCodes 0

    Write-ChocolateySuccess $packageName
} catch {
	Write-ChocolateyFailure $packageName $($_.Exception.Message)
	throw
}