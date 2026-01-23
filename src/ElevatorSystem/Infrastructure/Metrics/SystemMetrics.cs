using System.Threading;

namespace ElevatorSystem.Infrastructure.Metrics
{
    /// <summary>
    /// Thread-safe metrics implementation using atomic operations via Interlocked.
    /// No locks required for simple counter operations.
    /// </summary>
    public class SystemMetrics : IMetrics
    {
        private int _totalRequests;
        private int _acceptedRequests;
        private int _rejectedRequests;
        private int _completedHallCalls;
        private int _completedRequests;
        private int _rateLimitHits;
        private int _queueFullRejections;
        private int _safetyTimeoutHits;
        private int _pendingHallCalls;
        private int _activeElevators;

        public void IncrementTotalRequests() => Interlocked.Increment(ref _totalRequests);
        public void IncrementAcceptedRequests() => Interlocked.Increment(ref _acceptedRequests);
        public void IncrementRejectedRequests() => Interlocked.Increment(ref _rejectedRequests);
        public void IncrementCompletedHallCalls() => Interlocked.Increment(ref _completedHallCalls);
        public void IncrementCompletedRequests() => Interlocked.Increment(ref _completedRequests);
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
                CompletedRequests = _completedRequests,
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
