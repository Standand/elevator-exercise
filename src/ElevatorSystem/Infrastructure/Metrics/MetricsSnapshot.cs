namespace ElevatorSystem.Infrastructure.Metrics
{
    /// <summary>
    /// Snapshot of system metrics at a point in time.
    /// </summary>
    public class MetricsSnapshot
    {
        public int TotalRequests { get; set; }
        public int AcceptedRequests { get; set; }
        public int RejectedRequests { get; set; }
        public int CompletedHallCalls { get; set; }
        public int CompletedRequests { get; set; }
        public int RateLimitHits { get; set; }
        public int QueueFullRejections { get; set; }
        public int SafetyTimeoutHits { get; set; }
        public int PendingHallCalls { get; set; }
        public int ActiveElevators { get; set; }
        public int IdleElevators { get; set; }
    }
}
