# Runs dotnet test against an explicit list of test projects, rather than discovering every test
# project under src. The list comes from tools/select-test-projects.ps1, so a job only pays for the
# assemblies belonging to its test category.
#
# This is a scoped replacement for Particular/run-tests-action, which has no way to be told which
# projects to run. It should fold back into that action once it grows a 'projects' input.
#
# -MaxParallel runs several assemblies at once. CI jobs that merge categories sharing infrastructure
# use it so the job costs the slowest assembly rather than the sum of all of them. Output is buffered
# per run and replayed on completion, because interleaved dotnet test output is unreadable.

param(
    [Parameter(Mandatory)]
    [string]$Projects,

    [string]$TargetPlatform = 'x64',

    [ValidateRange(1, 16)]
    [int]$MaxParallel = 1,

    [switch]$ReportWarnings
)

$ErrorActionPreference = 'Stop'

$projectPaths = $Projects -split "`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ }

if ($projectPaths.Count -eq 0) {
    throw 'No test projects were supplied.'
}

Write-Output "Target Platform = $TargetPlatform"
Write-Output "Max parallel test runs = $MaxParallel"

$reportWarningsValue = if ($ReportWarnings) { 'true' } else { 'false' }
$isUnix = $PSVersionTable.Platform -eq 'Unix'

$runs = foreach ($project in $projectPaths) {
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

        [pscustomobject]@{
            Label     = "$(Split-Path $project -Leaf) ($framework)"
            Project   = $project
            Framework = $framework
        }
    }
}

$runs = @($runs)

if ($runs.Count -eq 0) {
    throw 'No test projects were runnable on this platform.'
}

# RavenDB.Embedded binds a fixed port, and the tests otherwise pick one by probing the active
# listeners, which concurrent processes all do at the same instant and all resolve to the same port.
# Hand each run its own instead. Left unset when running one at a time, because probing copes better
# with a port that something else on the machine already holds.
$assignPorts = $MaxParallel -gt 1
$nextPort = 33334
$portSpacing = 10

$exitCode = 0

function Complete-Run($run) {
    Write-Output "::group::Running $($run.Label)"
    foreach ($stream in @($run.OutFile, $run.ErrFile)) {
        if ((Test-Path $stream) -and (Get-Item $stream).Length -gt 0) {
            Get-Content -Path $stream | Write-Output
        }
        Remove-Item -Path $stream -Force -ErrorAction SilentlyContinue
    }
    Write-Output '::endgroup::'

    if ($run.Process.ExitCode -ne 0) {
        Write-Output "::error::$($run.Label) exit code = $($run.Process.ExitCode)"
        $script:exitCode = 1
    }
}

$pending = [Collections.Generic.Queue[object]]::new($runs)
$active = [Collections.Generic.List[object]]::new()

while ($pending.Count -gt 0 -or $active.Count -gt 0) {
    while ($active.Count -lt $MaxParallel -and $pending.Count -gt 0) {
        $run = $pending.Dequeue()
        $run | Add-Member -NotePropertyName OutFile -NotePropertyValue ([IO.Path]::GetTempFileName())
        $run | Add-Member -NotePropertyName ErrFile -NotePropertyValue ([IO.Path]::GetTempFileName())

        $arguments = @(
            'test', $run.Project
            '--configuration', 'Release'
            '--no-build'
            '--framework', $run.Framework
            '--logger', "GitHubActions;report-warnings=$reportWarningsValue"
            '--'
            'RunConfiguration.TreatNoTestsAsError=true'
            "RunConfiguration.TargetPlatform=$TargetPlatform"
        )

        if ($assignPorts) {
            # Set immediately before spawning, so the child inherits this run's value. Safe because
            # spawning is serialised here even though the runs themselves are not.
            $Env:ServiceControl_TESTS_RAVENDB_PORT = $nextPort
            Write-Output "Starting $($run.Label) with RavenDB port $nextPort"
            $nextPort += $portSpacing
        }
        else {
            Write-Output "Starting $($run.Label)"
        }

        $run | Add-Member -NotePropertyName Process -NotePropertyValue (
            Start-Process -FilePath 'dotnet' -ArgumentList $arguments -NoNewWindow -PassThru `
                -RedirectStandardOutput $run.OutFile -RedirectStandardError $run.ErrFile)
        $active.Add($run)
    }

    $finished = $active | Where-Object { $_.Process.HasExited }

    if (-not $finished) {
        Start-Sleep -Milliseconds 500
        continue
    }

    foreach ($run in @($finished)) {
        # Bounded on purpose. The parameterless WaitForExit() also waits for the redirected streams to
        # reach EOF, and on Linux Start-Process pumps them through a pipe, so a test that leaves behind
        # a child holding the inherited handle blocks it forever. HasExited has already told us the
        # test process itself is done; this only gives the pump a moment to drain.
        [void]$run.Process.WaitForExit(5000)
        Complete-Run $run
        [void]$active.Remove($run)
    }
}

exit $exitCode
