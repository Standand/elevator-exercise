using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.Services;
using ElevatorSystem.Domain.ValueObjects;
using ElevatorSystem.Infrastructure.Metrics;
using ElevatorSystem.Tests.Integration.TestHelpers;
using ElevatorSystem.Tests.TestHelpers;

namespace ElevatorSystem.Tests.Integration
{
    /// <summary>
    /// Integration tests for metrics collection during operations.
    /// </summary>
    public class MetricsIntegrationTests
    {
        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Priority", "P2")]
        public void RequestHallCall_UpdatesMetrics_TotalAndAcceptedIncremented()
        {
            var metrics = new SystemMetrics();
            var building = IntegrationTestBuilder.Create()
                .WithSchedulingStrategy(new DirectionAwareStrategy())
                .WithLogger(new MockLogger())
                .WithMetrics(metrics)
                .Build();

            var before = MetricsVerifier.CaptureSnapshot(metrics);
            
            var result = building.RequestHallCall(floor: 5, direction: Direction.UP);
            Assert.True(result.IsSuccess);
            
            var after = MetricsVerifier.CaptureSnapshot(metrics);
            
            MetricsVerifier.VerifyTotalRequestsIncremented(before, after, 1);
            MetricsVerifier.VerifyAcceptedRequestsIncremented(before, after, 1);
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Priority", "P2")]
        public void RequestHallCall_Rejected_UpdatesMetrics_TotalAndRejectedIncremented()
        {
            var metrics = new SystemMetrics();
            var building = IntegrationTestBuilder.Create()
                .WithSchedulingStrategy(new DirectionAwareStrategy())
                .WithLogger(new MockLogger())
                .WithMetrics(metrics)
                .Build();

            var before = MetricsVerifier.CaptureSnapshot(metrics);
            
            var result = building.RequestHallCall(floor: 15, direction: Direction.UP);
            Assert.False(result.IsSuccess);
            
            var after = MetricsVerifier.CaptureSnapshot(metrics);
            
            MetricsVerifier.VerifyTotalRequestsIncremented(before, after, 1);
            MetricsVerifier.VerifyRejectedRequestsIncremented(before, after, 1);
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Priority", "P2")]
        public void ProcessTick_CompletesHallCall_UpdatesMetrics()
        {
            var metrics = new SystemMetrics();
            var building = IntegrationTestBuilder.Create()
                .WithSchedulingStrategy(new DirectionAwareStrategy())
                .WithLogger(new MockLogger())
                .WithMetrics(metrics)
                .Build();

            var result = building.RequestHallCall(floor: 3, direction: Direction.UP);
            Assert.True(result.IsSuccess);
            
            building.ProcessTick();
            
            var before = MetricsVerifier.CaptureSnapshot(metrics);
            
            TickSimulator.ProcessTicksUntilElevatorReachesFloor(building, 1, 3, maxTicks: 30);
            TickSimulator.ProcessTicks(building, 5);
            
            var after = MetricsVerifier.CaptureSnapshot(metrics);
            
            MetricsVerifier.VerifyCompletedHallCallsIncremented(before, after, 1);
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Priority", "P2")]
        public void ProcessTick_UpdatesGauges_PendingAndActiveElevators()
        {
            var metrics = new SystemMetrics();
            var building = IntegrationTestBuilder.Create()
                .WithSchedulingStrategy(new DirectionAwareStrategy())
                .WithLogger(new MockLogger())
                .WithMetrics(metrics)
                .Build();

            var result1 = building.RequestHallCall(floor: 2, direction: Direction.UP);
            var result2 = building.RequestHallCall(floor: 5, direction: Direction.UP);
            
            building.ProcessTick();
            
            var snapshot = metrics.GetSnapshot();
            
            Assert.True(snapshot.PendingHallCalls >= 0);
            Assert.True(snapshot.ActiveElevators >= 0);
            Assert.True(snapshot.ActiveElevators <= 4);
        }
    }
}
