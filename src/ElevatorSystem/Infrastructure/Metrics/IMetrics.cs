namespace ElevatorSystem.Infrastructure.Metrics
{
    /// <summary>
    /// Interface for collecting and reporting system metrics.
    /// </summary>
    public interface IMetrics
    {
        // Counter methods
        void IncrementTotalRequests();
        void IncrementAcceptedRequests();
        void IncrementRejectedRequests();
        void IncrementCompletedHallCalls();
        void IncrementCompletedRequests();
        void IncrementRateLimitHits();
        void IncrementQueueFullRejections();
        void IncrementSafetyTimeoutHits();

        // Gauge methods
        void SetPendingHallCallsCount(int count);
        void SetActiveElevatorsCount(int count);

        // Snapshot method
        MetricsSnapshot GetSnapshot();
    }
}
