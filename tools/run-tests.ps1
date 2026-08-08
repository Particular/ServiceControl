# Runs dotnet test against an explicit list of test projects, rather than discovering every test
# project under src. The list comes from tools/select-test-projects.ps1, so a job only pays for the
# assemblies belonging to its test category.
#
# This is a scoped replacement for Particular/run-tests-action, which has no way to be told which
# projects to run. It should fold back into that action once it grows a 'projects' input.

param(
    [Parameter(Mandatory)]
    [string]$Projects,

    [string]$TargetPlatform = 'x64',

    [switch]$ReportWarnings
)

$ErrorActionPreference = 'Stop'

$projectPaths = $Projects -split "`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ }

if ($projectPaths.Count -eq 0) {
    throw 'No test projects were supplied.'
}

Write-Output "Target Platform = $TargetPlatform"

$reportWarningsValue = if ($ReportWarnings) { 'true' } else { 'false' }
$isUnix = $PSVersionTable.Platform -eq 'Unix'
$exitCode = 0

foreach ($project in $projectPaths) {
    $frameworks = @(
        (Select-Xml -Path $project -XPath "/Project/PropertyGroup/TargetFramework").Node.InnerText
        (Select-Xml -Path $project -XPath "/Project/PropertyGroup/TargetFrameworks").Node.InnerText -split ';'
    ) | Where-Object { $_ }

    if ($frameworks.Count -eq 0) {
        throw "Could not determine a target framework for $project."
    }

    foreach ($framework in $frameworks) {
        if ($isUnix -and ($framework.StartsWith('net4') -or $framework.Contains('-windows'))) {
            Write-Output "Skipping $(Split-Path $project -Leaf) ($framework) because it cannot run on this platform."
            continue
        }

        Write-Output "::group::Running $(Split-Path $project -Leaf) ($framework)"

        dotnet test $project --configuration Release --no-build --framework $framework --logger "GitHubActions;report-warnings=$reportWarningsValue" -- RunConfiguration.TreatNoTestsAsError=true "RunConfiguration.TargetPlatform=$TargetPlatform"

        Write-Output '::endgroup::'

        if ($LASTEXITCODE -ne 0) {
            Write-Output "::error::Exit code = $LASTEXITCODE"
            $exitCode = 1
        }
    }
}

exit $exitCode
