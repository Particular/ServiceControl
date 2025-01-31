# This script is used during CI to download a self-contained RavenDB server version to deploy/RavenDBServer
# so that it can be packaged with the ServiceControl installer.
#
# This is not necessary during local development, as the developer likely has the most recent version of
# .NET and ASP.NET runtimes installed and can use them to run RavenDB.Embedded
#
# The ServiceControl.Persistence.RavenDB project is responsible for turning the RavenDBServer into an
# artifact during development, but has a condition to skip that step on a CI server so that the
# self-contained version is used instead.

$version = (Select-Xml -Path src/Directory.Packages.props -XPath "/Project/ItemGroup/PackageVersion[@Include='RavenDB.Embedded']/@Version" | Select-Object -ExpandProperty Node).Value
Write-Output "In Directory.Packages.props, RavenDB.Embedded is using version '$version'"

$runningOnLinux = $PSVersionTable.Platform -eq 'Unix'
if ($runningOnLinux) {
  $os = "linux"
  $extension = "tar.bz2"
}
else {
  $os = "windows"
  $extension = "zip"
}

$downloadUrl = "https://daily-builds.s3.amazonaws.com/RavenDB-$($version)-$($os)-x64.$($extension)"
$tempPath = [System.IO.Path]::GetTempPath()
$zipPath = Join-Path $tempPath "ravendb.$($extension)"

Write-Output "Downloading RavenDB binaries from $downloadUrl to $zipPath"
Invoke-WebRequest $downloadUrl -OutFile $zipPath

$unzipPath = Join-Path $tempPath "ravendb-extracted"

Write-Output "Unzipping archive to $unzipPath"
if (Test-Path $unzipPath) { Remove-Item $unzipPath -Force -Recurse }
New-Item -ItemType Directory -Path $unzipPath

if ($runningOnLinux) {
  tar -jxf $zipPath -C $unzipPath
  $serverPath = Join-Path $unzipPath "RavenDB/Server"
}
else {
  Expand-Archive $zipPath $unzipPath
  $serverPath = Join-Path $unzipPath "Server"
}

$deployPath = Join-Path $PWD.Path deploy RavenDBServer
if (Test-Path $deployPath ) { Remove-Item $deployPath -Force -Recurse }
Write-Output "Copying '$serverPath' to '$deployPath'"
Copy-Item -Path $serverPath -Destination $deployPath -Recurse

Write-Output "Deleting temporary files"
Remove-Item $zipPath -Force
Remove-Item $unzipPath -Force -Recurse

Write-Output "Contents of deploy/RavenDBServer"
Get-ChildItem deploy/RavenDBServer
