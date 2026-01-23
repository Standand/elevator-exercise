using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.Services;
using ElevatorSystem.Domain.ValueObjects;
using ElevatorSystem.Infrastructure.Configuration;
using ElevatorSystem.Infrastructure.Metrics;
using ElevatorSystem.Tests.Integration.TestHelpers;
using ElevatorSystem.Tests.TestHelpers;

namespace ElevatorSystem.Tests.Integration
{
    /// <summary>
    /// Integration tests for error handling across layers.
    /// </summary>
    public class ErrorHandlingIntegrationTests
    {
        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Priority", "P2")]
        public void RequestHallCall_InvalidFloor_RejectsAndUpdatesMetrics()
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
            Assert.Contains("out of range", result.Error);
            
            var after = MetricsVerifier.CaptureSnapshot(metrics);
            MetricsVerifier.VerifyTotalRequestsIncremented(before, after, 1);
            MetricsVerifier.VerifyRejectedRequestsIncremented(before, after, 1);
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Priority", "P2")]
        public void RequestHallCall_InvalidDirection_RejectsAndUpdatesMetrics()
        {
            var metrics = new SystemMetrics();
            var building = IntegrationTestBuilder.Create()
                .WithSchedulingStrategy(new DirectionAwareStrategy())
                .WithLogger(new MockLogger())
                .WithMetrics(metrics)
                .Build();

            var before = MetricsVerifier.CaptureSnapshot(metrics);
            
            var result = building.RequestHallCall(floor: 5, direction: Direction.IDLE);
            
            Assert.False(result.IsSuccess);
            Assert.Contains("Invalid direction", result.Error);
            
            var after = MetricsVerifier.CaptureSnapshot(metrics);
            MetricsVerifier.VerifyTotalRequestsIncremented(before, after, 1);
            MetricsVerifier.VerifyRejectedRequestsIncremented(before, after, 1);
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Priority", "P2")]
        public void RequestPassengerJourney_SameSourceAndDestination_Rejects()
        {
            var metrics = new SystemMetrics();
            var building = IntegrationTestBuilder.Create()
                .WithSchedulingStrategy(new DirectionAwareStrategy())
                .WithLogger(new MockLogger())
                .WithMetrics(metrics)
                .Build();

            var before = MetricsVerifier.CaptureSnapshot(metrics);
            
            var result = building.RequestPassengerJourney(sourceFloor: 5, destinationFloor: 5);
            
            Assert.False(result.IsSuccess);
            Assert.Contains("cannot be the same", result.Error);
            
            var after = MetricsVerifier.CaptureSnapshot(metrics);
            MetricsVerifier.VerifyTotalRequestsIncremented(before, after, 1);
            MetricsVerifier.VerifyRejectedRequestsIncremented(before, after, 1);
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Priority", "P2")]
        public void RequestHallCall_QueueAtCapacity_RejectsAndUpdatesMetrics()
        {
            var config = new SimulationConfiguration
            {
                ElevatorCount = 4,
                MaxFloors = 5,
                DoorOpenTicks = 3,
                TickIntervalMs = 1000,
                RequestIntervalSeconds = 5
            };

            var metrics = new SystemMetrics();
            var building = IntegrationTestBuilder.Create()
                .WithSchedulingStrategy(new DirectionAwareStrategy())
                .WithConfiguration(config)
                .WithLogger(new MockLogger())
                .WithMetrics(metrics)
                .Build();

            var before = MetricsVerifier.CaptureSnapshot(metrics);
            
            for (int i = 0; i < 8; i++)
            {
                building.RequestHallCall(floor: i % 5, direction: i % 2 == 0 ? Direction.UP : Direction.DOWN);
            }
            
            var result = building.RequestHallCall(floor: 3, direction: Direction.UP);
            
            Assert.False(result.IsSuccess);
            Assert.Contains("at capacity", result.Error);
            
            var after = MetricsVerifier.CaptureSnapshot(metrics);
            Assert.True(after.QueueFullRejections >= 1);
        }
    }
}
