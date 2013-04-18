properties {
	$ProductVersion = "1.0"
	$PatchVersion = "0"
	$BuildNumber = "0"
	$PreRelease = ""
	$SignFile = if($env:SIGN_CER_PATH -ne $null) { $env:SIGN_CER_PATH } else { "" }
}

$baseDir = Split-Path (Resolve-Path $MyInvocation.MyCommand.Path)
$mergeModuleOutPutDir = "$baseDir\MergeModule\Output Package"
$toolsDir = "$baseDir\tools"
$mergeModuleProjectFile = "$baseDir\MergeModule\ManagementAPI.aip"
$setupProjectFile = "$baseDir\Setup\ManagementAPI.aip"
$setupModuleOutPutDir = "$baseDir\Setup\Output Package"

include $toolsDir\psake\buildutils.ps1

task default -depends Init, BuildMergeModule, SignMergeModule, BuildSetup, SignSetup

task Init {

	$sdkInstallRoot = Get-RegistryValue "HKLM:\SOFTWARE\Microsoft\Microsoft SDKs\Windows\v7.1" "InstallationFolder"
	if($sdkInstallRoot -eq $null) {
		$sdkInstallRoot = Get-RegistryValue "HKLM:\SOFTWARE\Microsoft\Microsoft SDKs\Windows\v7.0A" "InstallationFolder"
	}

	$script:signTool = $sdkInstallRoot + "Bin\signtool.exe"
	
    # Install path for Advanced Installer
    $AdvancedInstallerPath = ""
    $AdvancedInstallerPath = Get-RegistryValue "HKLM:\SOFTWARE\Wow6432Node\Caphyon\Advanced Installer\" "Advanced Installer Path" 
    $script:AdvinstCLI = $AdvancedInstallerPath + "\bin\x86\AdvancedInstaller.com"
    
}

task BuildMergeModule {  
       
	# Build setup with Advanced Installer	
   exec { &$script:AdvinstCLI /rebuild $mergeModuleProjectFile }
}

task BuildSetup {  
    
	copy $mergeModuleOutPutDir\*.* $baseDir\Setup\bundles
	
	if($PreRelease -eq "") {
		$archive = "NServiceBus.$ProductVersion.$PatchVersion" 
	} else {
		$archive = "NServiceBus.$ProductVersion.$PatchVersion-$PreRelease$BuildNumber"
	}

	# edit Advanced Installer Project	  
	exec { &$script:AdvinstCLI /edit $setupProjectFile /SetVersion "$ProductVersion.$PatchVersion" -noprodcode }	
	exec { &$script:AdvinstCLI /edit $setupProjectFile /SetPackageName "$archive.exe" -buildname DefaultBuild }
	
	# Build setup with Advanced Installer	
	exec { &$script:AdvinstCLI /rebuild $setupProjectFile }
}

task SignMergeModule -depends Init {
	if($SignFile -ne "") {
		exec { &$script:signTool sign /f "$SignFile" /p "$env:SIGN_CER_PASSWORD" /d "Management API Merge Module" /du "http://www.nservicebus.com" /q  $mergeModuleOutPutDir\*.* }
	}
}

task SignSetup -depends Init {
	if($SignFile -ne "") {
		exec { &$script:signTool sign /f "$SignFile" /p "$env:SIGN_CER_PASSWORD" /d "Management API" /du "http://www.nservicebus.com" /q  $setupModuleOutPutDir\*.* }
	}
}

