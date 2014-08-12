param(
    [parameter(Mandatory=$True)] [string]$baseDir,
    [parameter(Mandatory=$True)] [string]$version
)

function Get-RegistryValue($key, $value) {
    (Get-ItemProperty $key $value -ErrorAction SilentlyContinue).$value
}

#$baseDir = "%teamcity.build.checkoutDir%"
#$version = "%GitVersion.SemVer%"

Function CreateInstaller
{
    #until we figure out why AI looks in the wrong dir
    Copy-Item .\binaries\* .\installer\binaries

    $AdvancedInstallerPath = Get-RegistryValue "HKLM:\SOFTWARE\Wow6432Node\Caphyon\Advanced Installer\" "Advanced Installer Path" 

    $script:AdvinstCLI = $AdvancedInstallerPath + "bin\x86\AdvancedInstaller.com"

    $setupProjectFile = "$baseDir\installer\ServiceControl.aip"

    $packageName = "Particular.ServiceControl-$version.exe"

    # edit Advanced Installer Project   
    &$script:AdvinstCLI /edit $setupProjectFile /SetVersion $version
    &$script:AdvinstCLI /edit $setupProjectFile /SetPackageName $packageName -buildname DefaultBuild
        
    # Build setup with Advanced Installer 
    &$script:AdvinstCLI /rebuild $setupProjectFile
}

CreateInstaller