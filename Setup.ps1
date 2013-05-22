properties {
	$ProductVersion = "1.0"
	$PatchVersion = "0"
	$BuildNumber = if($env:BUILD_NUMBER -ne $null) { $env:BUILD_NUMBER } else { "0" }
	$PreRelease = ""
}

$baseDir = Split-Path (Resolve-Path $MyInvocation.MyCommand.Path)
$mergeModuleOutPutDir = "$baseDir\MergeModule\Output Package"
$toolsDir = "$baseDir\tools"
$mergeModuleProjectFile = "$baseDir\MergeModule\ManagementAPI.aip"
$setupProjectFile = "$baseDir\Setup\ManagementAPI.aip"
$setupModuleOutPutDir = "$baseDir\Setup\Output Package"

include $toolsDir\psake\buildutils.ps1

task default -depends Init, BuildMergeModule, BuildSetup

task Init {

    # Install path for Advanced Installer
    $AdvancedInstallerPath = ""
    $AdvancedInstallerPath = Get-RegistryValue "HKLM:\SOFTWARE\Wow6432Node\Caphyon\Advanced Installer\" "Advanced Installer Path" 
    $script:AdvinstCLI = $AdvancedInstallerPath + "\bin\x86\AdvancedInstaller.com"
    
}

task BuildMergeModule {  
       
	# Build setup with Advanced Installer
   exec { &$script:AdvinstCLI /edit $mergeModuleProjectFile /SetOutputLocation -buildname DefaultBuild -path "$mergeModuleOutPutDir" }	
   exec { &$script:AdvinstCLI /rebuild $mergeModuleProjectFile }
}

task BuildSetup {  
    
	robocopy "$mergeModuleOutPutDir" "$baseDir\Setup\bundles" *.msm
		
	if($PreRelease -eq "") {
		$archive = "ParticularManagement.$ProductVersion.$PatchVersion" 
	} else {
		$archive = "ParticularManagement.$ProductVersion.$PatchVersion-$PreRelease$BuildNumber"
	}

	# edit Advanced Installer Project	  
	exec { &$script:AdvinstCLI /edit $setupProjectFile /SetVersion "$ProductVersion.$PatchVersion" -noprodcode }	
	exec { &$script:AdvinstCLI /edit $setupProjectFile /SetPackageName "$archive.exe" -buildname DefaultBuild }
	exec { &$script:AdvinstCLI /edit $setupProjectFile /SetOutputLocation -buildname DefaultBuild -path "$setupModuleOutPutDir" }
	# Build setup with Advanced Installer	
	exec { &$script:AdvinstCLI /rebuild $setupProjectFile }
}

