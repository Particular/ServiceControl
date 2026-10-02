namespace TestingTool;

/// <summary>
/// Shared singleton holding live counters from all subsystems (scenario runner, replay, search).
/// Provides a single read point for the <c>GET /api/status</c> endpoint without needing to resolve
/// individual hosted service instances.
/// </summary>
public sealed class TestingToolMetrics
{
    private long _totalErrorsSent;
    private long _totalErrorsReplayed;
    private long _totalErrorsArchived;
    private long _totalSearches;
    private long _totalRetentionSweepsStarted;
    private long _totalRetentionSweepsAlreadyRunning;
    private long _totalRetentionSweepsNotSupported;
    private long _totalBypassErrorsWritten;
    private long _totalBypassErrorsFailed;
    private long _totalCustomCheckFailures;
    private long _activeScenarios;
    private double _currentRate;

    public long TotalErrorsSent => Interlocked.Read(ref _totalErrorsSent);
    public long TotalErrorsReplayed => Interlocked.Read(ref _totalErrorsReplayed);
    public long TotalErrorsArchived => Interlocked.Read(ref _totalErrorsArchived);
    public long TotalSearches => Interlocked.Read(ref _totalSearches);
    public long TotalRetentionSweepsStarted => Interlocked.Read(ref _totalRetentionSweepsStarted);
    public long TotalRetentionSweepsAlreadyRunning => Interlocked.Read(ref _totalRetentionSweepsAlreadyRunning);
    public long TotalRetentionSweepsNotSupported => Interlocked.Read(ref _totalRetentionSweepsNotSupported);
    public long TotalBypassErrorsWritten => Interlocked.Read(ref _totalBypassErrorsWritten);
    public long TotalBypassErrorsFailed => Interlocked.Read(ref _totalBypassErrorsFailed);
    public long TotalCustomCheckFailures => Interlocked.Read(ref _totalCustomCheckFailures);
    public int ActiveScenarios => (int)Interlocked.Read(ref _activeScenarios);
    public double CurrentRate => _currentRate;

    public void AddErrorsSent(long count) => Interlocked.Add(ref _totalErrorsSent, count);
    public void AddErrorsReplayed(long count) => Interlocked.Add(ref _totalErrorsReplayed, count);
    public void AddErrorsArchived(long count) => Interlocked.Add(ref _totalErrorsArchived, count);
    public void AddSearches(long count) => Interlocked.Add(ref _totalSearches, count);
    public void AddRetentionSweepsStarted(long count) => Interlocked.Add(ref _totalRetentionSweepsStarted, count);
    public void AddRetentionSweepsAlreadyRunning(long count) => Interlocked.Add(ref _totalRetentionSweepsAlreadyRunning, count);
    public void AddRetentionSweepsNotSupported(long count) => Interlocked.Add(ref _totalRetentionSweepsNotSupported, count);
    public void AddBypassErrorsWritten(long count) => Interlocked.Add(ref _totalBypassErrorsWritten, count);
    public void AddBypassErrorsFailed(long count) => Interlocked.Add(ref _totalBypassErrorsFailed, count);
    public void AddCustomCheckFailures(long count) => Interlocked.Add(ref _totalCustomCheckFailures, count);
    public void SetActiveScenarios(int count) => Interlocked.Exchange(ref _activeScenarios, count);
    public void SetCurrentRate(double rate) => _currentRate = rate;
}