using System.Threading;

namespace ElevatorSystem.Infrastructure.Metrics
{
    /// <summary>
    /// Thread-safe metrics implementation using atomic operations.
    /// </summary>
    public class SystemMetrics : IMetrics
    {
        // Counters (atomic)
        private int _totalRequests = 0;
        private int _acceptedRequests = 0;
        private int _rejectedRequests = 0;
        private int _completedHallCalls = 0;
        private int _rateLimitHits = 0;
        private int _queueFullRejections = 0;
        private int _safetyTimeoutHits = 0;

        // Gauges (set by Building)
        private int _pendingHallCalls = 0;
        private int _activeElevators = 0;

        public void IncrementTotalRequests() => Interlocked.Increment(ref _totalRequests);
        public void IncrementAcceptedRequests() => Interlocked.Increment(ref _acceptedRequests);
        public void IncrementRejectedRequests() => Interlocked.Increment(ref _rejectedRequests);
        public void IncrementCompletedHallCalls() => Interlocked.Increment(ref _completedHallCalls);
        public void IncrementRateLimitHits() => Interlocked.Increment(ref _rateLimitHits);
        public void IncrementQueueFullRejections() => Interlocked.Increment(ref _queueFullRejections);
        public void IncrementSafetyTimeoutHits() => Interlocked.Increment(ref _safetyTimeoutHits);

        public void SetPendingHallCallsCount(int count) => Interlocked.Exchange(ref _pendingHallCalls, count);
        public void SetActiveElevatorsCount(int count) => Interlocked.Exchange(ref _activeElevators, count);

        public MetricsSnapshot GetSnapshot()
        {
            return new MetricsSnapshot
            {
                TotalRequests = _totalRequests,
                AcceptedRequests = _acceptedRequests,
                RejectedRequests = _rejectedRequests,
                CompletedHallCalls = _completedHallCalls,
                RateLimitHits = _rateLimitHits,
                QueueFullRejections = _queueFullRejections,
                SafetyTimeoutHits = _safetyTimeoutHits,
                PendingHallCalls = _pendingHallCalls,
                ActiveElevators = _activeElevators,
                IdleElevators = 0 // Computed by caller if needed
            };
        }
    }
}
