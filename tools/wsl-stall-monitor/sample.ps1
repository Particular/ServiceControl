$ErrorActionPreference = 'Stop'

$counters = @(
    '\Memory\Available Bytes'
    '\Memory\Committed Bytes'
    '\Memory\Commit Limit'
    '\Processor(_Total)\% Processor Time'
    '\PhysicalDisk(_Total)\Avg. Disk sec/Read'
    '\PhysicalDisk(_Total)\Avg. Disk sec/Write'
    '\PhysicalDisk(_Total)\Current Disk Queue Length'
)
$values = @{}
foreach ($sample in (Get-Counter -Counter $counters -MaxSamples 1).CounterSamples) {
    $values[($sample.Path -replace '^\\\\[^\\]+', '')] = $sample.CookedValue
}
foreach ($counter in $counters) {
    if (-not $values.ContainsKey($counter)) {
        throw "Missing performance counter: $counter"
    }
}
$processes = Get-Process |
    Where-Object { $_.ProcessName -match '^(dotnet|MSBuild|VBCSCompiler|wsl.*|vmmem.*|node|pwsh|Runner\..*)$' } |
    Select-Object Id, ProcessName, @{ Name = 'CpuSeconds'; Expression = { $_.CPU } }, WorkingSet64, PrivateMemorySize64

[ordered]@{
    utc = [DateTimeOffset]::UtcNow.ToString('o')
    availableMemoryBytes = $values['\Memory\Available Bytes']
    committedMemoryBytes = $values['\Memory\Committed Bytes']
    commitLimitBytes = $values['\Memory\Commit Limit']
    cpuPercent = $values['\Processor(_Total)\% Processor Time']
    diskReadLatencySeconds = $values['\PhysicalDisk(_Total)\Avg. Disk sec/Read']
    diskWriteLatencySeconds = $values['\PhysicalDisk(_Total)\Avg. Disk sec/Write']
    diskQueueLength = $values['\PhysicalDisk(_Total)\Current Disk Queue Length']
    processes = @($processes)
} | ConvertTo-Json -Depth 4 -Compress
