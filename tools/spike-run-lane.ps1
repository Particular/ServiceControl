# Spike support script for the "background lanes" CI experiment.
# Runs one test category lane: an explicit list of test projects with the
# ServiceControl_TESTS_FILTER env var set, mirroring what run-tests-action does
# on master. Touches a sentinel file when done so the memory sampler can stop.
# Exit code reflects any failing project, but the sentinel is written either way.

param(
    [Parameter(Mandatory)]
    [string]$Name,

    [Parameter(Mandatory)]
    [string[]]$Projects,

    [string]$Filter = 'Default'
)

$ErrorActionPreference = 'Continue'

$env:ServiceControl_TESTS_FILTER = $Filter
Write-Output "Lane '$Name' filter='$Filter' projects=$($Projects.Count)"

$exitCode = 0

foreach ($project in $Projects) {
    Write-Output "::group::$Name :: $project"
    dotnet test $project --configuration Release --no-build --logger "GitHubActions"
    if ($LASTEXITCODE -ne 0) {
        Write-Output "::error::$Name :: $project exited with $LASTEXITCODE"
        $exitCode = 1
    }
    Write-Output "::endgroup::"
}

New-Item -ItemType File -Path "sentinel-$Name" -Force | Out-Null

exit $exitCode