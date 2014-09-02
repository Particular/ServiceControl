#ensure a powershell error sends exitcode to Octopus
trap { 
    Write-Host $_
    Exit 1
}

# Passed from Octopus  
$requiredvariables = "ghusername", "ghpassword", "mygetkey"
$requiredvariables | % {
    if (!(Test-Path "variable:$_")) {
        throw "Variable $_ has not been set in Octopus config" 
    }
}

#content folder in nuget package contains files to upload
push-location .\content

#rename .zip files back to .nupkg
Get-ChildItem -Path ".\*" -Include "*.nzip" | Rename-Item -NewName { $_.BaseName }

Write-Host "Creating release for milestone {{milestone}} ..."

& "..\tools\ReleaseNotesCompiler.CLI.exe" $ghusername $ghpassword "particular" "servicecontrol" "{{milestone}}" "Particular.ServiceControl-{{milestone}}.exe"

$files = Get-ChildItem -Path ".\*" -Include "*.nupkg"
foreach ($file in $files) { 
    $fileName =  $file.Name

    & "..\tools\NuGet.exe" push $fileName $mygetkey -Source https://www.myget.org/F/particular-chocolatey/api/v2/package
}
