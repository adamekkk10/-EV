namespace PlusEV.Application.Services;

/// <summary>
/// Rolling latency estimates. <see cref="RecordFetch"/> is called whenever a snapshot
/// comes back from a provider; <see cref="RecordIdentify"/> is called each time the EV
/// engine identifies an opportunity. The UI status bar reads the averages.
/// </summary>
public sealed class LatencyTracker
{
    private readonly object _lock = new();
    private readonly Queue<double> _fetchMs = new();
    private readonly Queue<double> _identifyMs = new();
    private const int WindowSize = 100;

    public int FetchSampleCount { get { lock (_lock) return _fetchMs.Count; } }
    public double AverageFetchLatencyMs { get { lock (_lock) return _fetchMs.Count == 0 ? 0d : _fetchMs.Average(); } }
    public double AverageIdentifyLatencyMs { get { lock (_lock) return _identifyMs.Count == 0 ? 0d : _identifyMs.Average(); } }

    public void RecordFetch(TimeSpan bookToFetch) => Record(_fetchMs, bookToFetch.TotalMilliseconds);
    public void RecordIdentify(TimeSpan fetchToIdentify) => Record(_identifyMs, fetchToIdentify.TotalMilliseconds);

    private void Record(Queue<double> q, double value)
    {
        lock (_lock)
        {
            q.Enqueue(value);
            while (q.Count > WindowSize) q.Dequeue();
        }
    }
}
