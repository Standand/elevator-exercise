using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using ElevatorSystem.Infrastructure.Metrics;

namespace ElevatorSystem.Tests.Infrastructure.Metrics
{
    /// <summary>
    /// Tests for SystemMetrics thread-safe counters.
    /// Validates atomic operations and concurrent updates.
    /// </summary>
    public class SystemMetricsTests
    {
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P3")]
        public void IncrementCounters_UpdatesSnapshot()
        {
            // Arrange
            var metrics = new SystemMetrics();
            
            // Act
            metrics.IncrementTotalRequests();
            metrics.IncrementAcceptedRequests();
            metrics.IncrementCompletedHallCalls();
            var snapshot = metrics.GetSnapshot();
            
            // Assert
            Assert.Equal(1, snapshot.TotalRequests);
            Assert.Equal(1, snapshot.AcceptedRequests);
            Assert.Equal(1, snapshot.CompletedHallCalls);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P3")]
        public void SetGauges_UpdatesSnapshot()
        {
            // Arrange
            var metrics = new SystemMetrics();
            
            // Act
            metrics.SetPendingHallCallsCount(5);
            metrics.SetActiveElevatorsCount(3);
            var snapshot = metrics.GetSnapshot();
            
            // Assert
            Assert.Equal(5, snapshot.PendingHallCalls);
            Assert.Equal(3, snapshot.ActiveElevators);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P3")]
        public void Metrics_ThreadSafe_ConcurrentIncrements()
        {
            // Arrange
            var metrics = new SystemMetrics();
            var tasks = new List<Task>();
            
            // Act - 10 threads, each incrementing 100 times
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    for (int j = 0; j < 100; j++)
                    {
                        metrics.IncrementTotalRequests();
                    }
                }));
            }
            Task.WaitAll(tasks.ToArray());
            var snapshot = metrics.GetSnapshot();
            
            // Assert - No lost increments
            Assert.Equal(1000, snapshot.TotalRequests);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P3")]
        public void AllMetrics_IndependentCounters()
        {
            // Arrange
            var metrics = new SystemMetrics();
            
            // Act
            metrics.IncrementTotalRequests();
            metrics.IncrementTotalRequests();
            metrics.IncrementAcceptedRequests();
            metrics.IncrementRejectedRequests();
            metrics.IncrementCompletedHallCalls();
            metrics.IncrementRateLimitHits();
            metrics.IncrementQueueFullRejections();
            metrics.IncrementSafetyTimeoutHits();
            
            var snapshot = metrics.GetSnapshot();
            
            // Assert
            Assert.Equal(2, snapshot.TotalRequests);
            Assert.Equal(1, snapshot.AcceptedRequests);
            Assert.Equal(1, snapshot.RejectedRequests);
            Assert.Equal(1, snapshot.CompletedHallCalls);
            Assert.Equal(1, snapshot.RateLimitHits);
            Assert.Equal(1, snapshot.QueueFullRejections);
            Assert.Equal(1, snapshot.SafetyTimeoutHits);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P3")]
        public void GetSnapshot_ReturnsConsistentState()
        {
            // Arrange
            var metrics = new SystemMetrics();
            metrics.IncrementTotalRequests();
            metrics.IncrementAcceptedRequests();
            metrics.SetPendingHallCallsCount(3);
            
            // Act
            var snapshot1 = metrics.GetSnapshot();
            var snapshot2 = metrics.GetSnapshot();
            
            // Assert - Multiple snapshots return same values
            Assert.Equal(snapshot1.TotalRequests, snapshot2.TotalRequests);
            Assert.Equal(snapshot1.AcceptedRequests, snapshot2.AcceptedRequests);
            Assert.Equal(snapshot1.PendingHallCalls, snapshot2.PendingHallCalls);
        }
    }
}
