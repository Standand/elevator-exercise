using ElevatorSystem.Infrastructure.Metrics;

namespace ElevatorSystem.Tests.Integration.TestHelpers
{
    /// <summary>
    /// Helper for verifying metrics changes in integration tests.
    /// </summary>
    public static class MetricsVerifier
    {
        public static MetricsSnapshot CaptureSnapshot(IMetrics metrics)
        {
            return metrics.GetSnapshot();
        }

        public static void VerifyIncrement(MetricsSnapshot before, MetricsSnapshot after, Func<MetricsSnapshot, int> getValue, int expectedIncrement)
        {
            var beforeValue = getValue(before);
            var afterValue = getValue(after);
            var actualIncrement = afterValue - beforeValue;
            Assert.Equal(expectedIncrement, actualIncrement);
        }

        public static void VerifyTotalRequestsIncremented(MetricsSnapshot before, MetricsSnapshot after, int expectedIncrement = 1)
        {
            VerifyIncrement(before, after, s => s.TotalRequests, expectedIncrement);
        }

        public static void VerifyAcceptedRequestsIncremented(MetricsSnapshot before, MetricsSnapshot after, int expectedIncrement = 1)
        {
            VerifyIncrement(before, after, s => s.AcceptedRequests, expectedIncrement);
        }

        public static void VerifyRejectedRequestsIncremented(MetricsSnapshot before, MetricsSnapshot after, int expectedIncrement = 1)
        {
            VerifyIncrement(before, after, s => s.RejectedRequests, expectedIncrement);
        }

        public static void VerifyCompletedHallCallsIncremented(MetricsSnapshot before, MetricsSnapshot after, int expectedIncrement = 1)
        {
            VerifyIncrement(before, after, s => s.CompletedHallCalls, expectedIncrement);
        }
    }
}
