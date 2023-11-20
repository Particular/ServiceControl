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

$downloadUrl = "https://daily-builds.s3.amazonaws.com/RavenDB-$($version)-windows-x64.zip"
$zipPath = Join-Path $Env:TEMP "ravendb.zip"
Write-Output "Downloading RavenDB binaries from $downloadUrl to $zipPath"
Invoke-WebRequest $downloadUrl -OutFile $zipPath

Write-Output "Unzipping archive..."
$unzipPath = Join-Path $Env:TEMP "ravendb-extracted"
Remove-Item $unzipPath -Force -Recurse
Expand-Archive $zipPath $unzipPath

$serverPath = Join-Path $unzipPath "Server"
Remove-Item deploy/RavenDBServer -Force -Recurse
Write-Output "Copying $serverPath to deploy/RavenDBServer"
Copy-Item -Path $serverPath -Destination "deploy/RavenDBServer" -Recurse

Write-Output "Deleting temporary files"
Remove-Item $zipPath -Force
Remove-Item $unzipPath -Force -Recurse
