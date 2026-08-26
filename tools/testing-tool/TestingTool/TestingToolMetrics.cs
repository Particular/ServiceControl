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
    private long _totalSearches;
    private long _totalBypassErrorsWritten;
    private long _activeScenarios;
    private double _currentRate;

    public long TotalErrorsSent => Interlocked.Read(ref _totalErrorsSent);
    public long TotalErrorsReplayed => Interlocked.Read(ref _totalErrorsReplayed);
    public long TotalSearches => Interlocked.Read(ref _totalSearches);
    public long TotalBypassErrorsWritten => Interlocked.Read(ref _totalBypassErrorsWritten);
    public int ActiveScenarios => (int)Interlocked.Read(ref _activeScenarios);
    public double CurrentRate => _currentRate;

    public void AddErrorsSent(long count) => Interlocked.Add(ref _totalErrorsSent, count);
    public void AddErrorsReplayed(long count) => Interlocked.Add(ref _totalErrorsReplayed, count);
    public void AddSearches(long count) => Interlocked.Add(ref _totalSearches, count);
    public void AddBypassErrorsWritten(long count) => Interlocked.Add(ref _totalBypassErrorsWritten, count);
    public void SetActiveScenarios(int count) => Interlocked.Exchange(ref _activeScenarios, count);
    public void SetCurrentRate(double rate) => _currentRate = rate;
}