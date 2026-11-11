# This script is used during CI to download the self-contained RavenDB servers that are packaged with the ServiceControl installer:
#   - the RavenDB 6.2 server (the RavenDB6Version property in src/Directory.Packages.props) to deploy/RavenDBServer, for the RavenDB persister
#   - the RavenDB 7.2 server (the central RavenDB.Embedded version) to deploy/RavenDB7Server, for the RavenDB7 persister
#
# This is not necessary during local development, as the developer likely has the most recent version of
# .NET and ASP.NET runtimes installed and can use them to run RavenDB.Embedded
#
# The ServiceControl.Persistence.RavenDB6 and ServiceControl.Persistence.RavenDB projects are responsible for turning
# the RavenDBServer and RavenDB7Server into artifacts during development, but have a condition to skip that step
# on a CI server so that the self-contained versions are used instead.

$packagesPropsPath = "src/Directory.Packages.props"

$ravenDB7Version = (Select-Xml -Path $packagesPropsPath -XPath "/Project/ItemGroup/PackageVersion[@Include='RavenDB.Embedded']/@Version" | Select-Object -ExpandProperty Node).Value
Write-Output "In Directory.Packages.props, RavenDB.Embedded is using version '$ravenDB7Version'"

$ravenDB6Version = (Select-Xml -Path $packagesPropsPath -XPath "/Project/PropertyGroup/RavenDB6Version" | Select-Object -ExpandProperty Node).InnerText
Write-Output "In Directory.Packages.props, RavenDB6Version is '$ravenDB6Version'"

if ([string]::IsNullOrWhiteSpace($ravenDB7Version) -or [string]::IsNullOrWhiteSpace($ravenDB6Version)) {
  throw "Could not read the RavenDB.Embedded version and the RavenDB6Version property from $packagesPropsPath"
}

$runningOnLinux = $PSVersionTable.Platform -eq 'Unix'
if ($runningOnLinux) {
  $os = "linux"
  $extension = "tar.bz2"
}
else {
  $os = "windows"
  $extension = "zip"
}

$tempPath = [System.IO.Path]::GetTempPath()

function Install-RavenDBServer {
  param (
    [Parameter(Mandatory = $true)] [string] $Version,
    [Parameter(Mandatory = $true)] [string] $DeployFolderName
  )

  $downloadUrl = "https://daily-builds.s3.amazonaws.com/RavenDB-$($Version)-$($os)-x64.$($extension)"
  $zipPath = Join-Path $tempPath "ravendb-$($Version).$($extension)"

  Write-Output "Downloading RavenDB $Version binaries from $downloadUrl to $zipPath"
  Invoke-WebRequest $downloadUrl -OutFile $zipPath

  $unzipPath = Join-Path $tempPath "ravendb-extracted-$($Version)"

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

  $deployPath = Join-Path $PWD.Path deploy $DeployFolderName
  if (Test-Path $deployPath ) { Remove-Item $deployPath -Force -Recurse }
  Write-Output "Copying '$serverPath' to '$deployPath'"
  Copy-Item -Path $serverPath -Destination $deployPath -Recurse

  Write-Output "Deleting temporary files"
  Remove-Item $zipPath -Force
  Remove-Item $unzipPath -Force -Recurse
}

Install-RavenDBServer -Version $ravenDB6Version -DeployFolderName "RavenDBServer"
Install-RavenDBServer -Version $ravenDB7Version -DeployFolderName "RavenDB7Server"
