
$packageName = "ServiceControl"

$url = gci -path "c:\ChocolateyResourceCache" -Filter "Particular.$packageName-*.exe" -ErrorAction SilentlyContinue| select -first 1

if($url){
	$url = $url | Select -expandProperty FullName
}
else{
	$url = "https://github.com/Particular/$packageName/releases/download/{{ReleaseName}}/Particular.$packageName-{{FileVersion}}.exe"
}


try {

    $existngService = Get-Service -Name "Particular.Management" -ErrorAction SilentlyContinue
    if ($existngService) {
        throw "The 'Particular Management' service must be uninstalled prior to installing Service Control"
    }

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
    Remove-Item $file -ErrorAction SilentlyContinue 
} catch {
	Write-ChocolateyFailure $packageName $($_.Exception.Message)
	throw
}