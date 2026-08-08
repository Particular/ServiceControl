# Spike support script for the "background lanes" CI experiment.
# Samples available physical memory every 5 seconds and writes a CSV, so we can
# see whether concurrent acceptance-test lanes fit in the 16 GB runner. Stops
# early once every lane (5 sentinel-* files) has finished, or after MaxSeconds.

param(
    [int]$MaxSeconds = 1800
)

$log = 'spike-memory.csv'
$start = Get-Date
$minAvailableGb = [double]::MaxValue
$samples = 0

while (((Get-Date) - $start).TotalSeconds -lt $MaxSeconds) {
    try {
        $availableGb = (Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory / 1MB
    }
    catch {
        $availableGb = -1
    }

    "$(Get-Date -Format HH:mm:ss),$([math]::Round($availableGb, 1))" | Add-Content -Path $log
    if ($availableGb -ge 0 -and $availableGb -lt $minAvailableGb) {
        $minAvailableGb = $availableGb
    }
    $samples++

    $doneLanes = @(Get-ChildItem -Path . -Filter 'sentinel-*' -ErrorAction SilentlyContinue)
    if ($doneLanes.Count -ge 5) {
        break
    }

    Start-Sleep -Seconds 5
}

Write-Output "Memory samples: $samples, min available: $([math]::Round($minAvailableGb, 1)) GB"
Get-Content -Path $log | Select-Object -Last 5