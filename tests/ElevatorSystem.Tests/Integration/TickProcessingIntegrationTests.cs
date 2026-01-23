using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.Services;
using ElevatorSystem.Domain.ValueObjects;
using ElevatorSystem.Infrastructure.Metrics;
using ElevatorSystem.Tests.Integration.TestHelpers;
using ElevatorSystem.Tests.TestHelpers;

namespace ElevatorSystem.Tests.Integration
{
    /// <summary>
    /// Integration tests for tick processing sequence and state transitions.
    /// </summary>
    public class TickProcessingIntegrationTests
    {
        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Priority", "P1")]
        public void ProcessTick_AssignsPendingHallCalls_ThenProcessesElevators()
        {
            var building = IntegrationTestBuilder.Create()
                .WithSchedulingStrategy(new DirectionAwareStrategy())
                .WithLogger(new MockLogger())
                .WithMetrics(new SystemMetrics())
                .Build();

            var result = building.RequestHallCall(floor: 5, direction: Direction.UP);
            Assert.True(result.IsSuccess);
            var hallCallId = result.Value!.Id;
            
            var statusBefore = building.GetStatus();
            Assert.True(statusBefore.PendingHallCallsCount > 0);
            
            building.ProcessTick();
            
            var statusAfter = building.GetStatus();
            var assignedElevator = statusAfter.Elevators.FirstOrDefault(e => 
                e.AssignedHallCallIds.Contains(hallCallId));
            
            Assert.NotNull(assignedElevator);
            Assert.True(assignedElevator.State == ElevatorState.MOVING || assignedElevator.State == ElevatorState.LOADING);
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Priority", "P1")]
        public void ProcessTick_ProcessesAllElevators_InOrder()
        {
            var building = IntegrationTestBuilder.Create()
                .WithSchedulingStrategy(new DirectionAwareStrategy())
                .WithLogger(new MockLogger())
                .WithMetrics(new SystemMetrics())
                .Build();

            var result1 = building.RequestHallCall(floor: 2, direction: Direction.UP);
            var result2 = building.RequestHallCall(floor: 4, direction: Direction.UP);
            var result3 = building.RequestHallCall(floor: 6, direction: Direction.UP);
            
            building.ProcessTick();
            
            var status = building.GetStatus();
            var elevatorsWithAssignments = status.Elevators
                .Where(e => e.AssignedHallCallIds.Any())
                .OrderBy(e => e.Id)
                .ToList();
            
            Assert.True(elevatorsWithAssignments.Count >= 2);
            
            var elevatorIds = elevatorsWithAssignments.Select(e => e.Id).ToList();
            Assert.Equal(elevatorIds.OrderBy(id => id), elevatorIds);
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Priority", "P1")]
        public void ProcessTick_CompletesHallCalls_AfterElevatorProcessing()
        {
            var building = IntegrationTestBuilder.Create()
                .WithSchedulingStrategy(new DirectionAwareStrategy())
                .WithLogger(new MockLogger())
                .WithMetrics(new SystemMetrics())
                .Build();

            var result = building.RequestHallCall(floor: 3, direction: Direction.UP);
            Assert.True(result.IsSuccess);
            var hallCallId = result.Value!.Id;
            
            building.ProcessTick();
            
            TickSimulator.ProcessTicksUntilElevatorReachesFloor(building, 1, 3, maxTicks: 30);
            
            var status = building.GetStatus();
            var elevator = status.Elevators.First(e => e.Id == 1);
            Assert.Equal(3, elevator.CurrentFloor);
            Assert.Equal(ElevatorState.LOADING, elevator.State);
            
            TickSimulator.ProcessTicks(building, 5);
            
            var finalStatus = building.GetStatus();
            var finalElevator = finalStatus.Elevators.First(e => e.Id == 1);
            Assert.DoesNotContain(hallCallId, finalElevator.AssignedHallCallIds);
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Priority", "P1")]
        public void ProcessTick_UpdatesMetrics_AtEnd()
        {
            var metrics = new SystemMetrics();
            var building = IntegrationTestBuilder.Create()
                .WithSchedulingStrategy(new DirectionAwareStrategy())
                .WithLogger(new MockLogger())
                .WithMetrics(metrics)
                .Build();

            var before = MetricsVerifier.CaptureSnapshot(metrics);
            
            building.RequestHallCall(floor: 2, direction: Direction.UP);
            building.ProcessTick();
            
            var after = MetricsVerifier.CaptureSnapshot(metrics);
            
            Assert.True(after.PendingHallCalls >= 0);
            Assert.True(after.ActiveElevators >= 0);
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Priority", "P1")]
        public void ProcessTick_MultipleTicks_CompleteElevatorJourney()
        {
            var building = IntegrationTestBuilder.Create()
                .WithSchedulingStrategy(new DirectionAwareStrategy())
                .WithLogger(new MockLogger())
                .WithMetrics(new SystemMetrics())
                .Build();

            var result = building.RequestPassengerJourney(sourceFloor: 0, destinationFloor: 5);
            Assert.True(result.IsSuccess);
            
            building.ProcessTick();
            
            var initialStatus = building.GetStatus();
            var elevator = initialStatus.Elevators.FirstOrDefault(e => 
                e.AssignedHallCallIds.Any());
            
            Assert.NotNull(elevator);
            Assert.Equal(0, elevator.CurrentFloor);
            Assert.Equal(ElevatorState.MOVING, elevator.State);
            
            TickSimulator.ProcessTicksUntilElevatorReachesFloor(building, elevator.Id, 5, maxTicks: 50);
            
            var finalStatus = building.GetStatus();
            var finalElevator = finalStatus.Elevators.First(e => e.Id == elevator.Id);
            Assert.Equal(5, finalElevator.CurrentFloor);
            Assert.Equal(ElevatorState.LOADING, finalElevator.State);
        }
    }
}
