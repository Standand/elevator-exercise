using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.Services;
using ElevatorSystem.Domain.ValueObjects;
using ElevatorSystem.Infrastructure.Metrics;
using ElevatorSystem.Tests.Integration.TestHelpers;
using ElevatorSystem.Tests.TestHelpers;

namespace ElevatorSystem.Tests.Integration
{
    /// <summary>
    /// Integration tests for complete request lifecycle from creation to completion.
    /// </summary>
    public class RequestLifecycleIntegrationTests
    {
        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Priority", "P1")]
        public void RequestPassengerJourney_CompleteFlow_FromRequestToCompletion()
        {
            var building = IntegrationTestBuilder.Create()
                .WithSchedulingStrategy(new DirectionAwareStrategy())
                .WithLogger(new MockLogger())
                .WithMetrics(new SystemMetrics())
                .Build();

            var result = building.RequestPassengerJourney(sourceFloor: 0, destinationFloor: 5);
            
            Assert.True(result.IsSuccess);
            var request = result.Value!;
            
            building.ProcessTick();
            
            var status = building.GetStatus();
            var assignedElevator = status.Elevators.FirstOrDefault(e => 
                e.AssignedHallCallIds.Any());
            
            Assert.NotNull(assignedElevator);
            Assert.Contains(0, assignedElevator.Destinations);
            Assert.Contains(5, assignedElevator.Destinations);
            
            TickSimulator.ProcessTicksUntilElevatorReachesFloor(building, assignedElevator.Id, 0, maxTicks: 20);
            
            var statusAtSource = building.GetStatus();
            var elevatorAtSource = statusAtSource.Elevators.First(e => e.Id == assignedElevator.Id);
            Assert.Equal(0, elevatorAtSource.CurrentFloor);
            Assert.Equal(ElevatorState.LOADING, elevatorAtSource.State);
            
            TickSimulator.ProcessTicksUntilElevatorReachesFloor(building, assignedElevator.Id, 5, maxTicks: 30);
            
            var finalStatus = building.GetStatus();
            var finalElevator = finalStatus.Elevators.First(e => e.Id == assignedElevator.Id);
            Assert.Equal(5, finalElevator.CurrentFloor);
            Assert.Equal(ElevatorState.LOADING, finalElevator.State);
            Assert.DoesNotContain(5, finalElevator.Destinations);
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Priority", "P1")]
        public void RequestPassengerJourney_MultiplePassengersSameHallCall_AllDestinationsAdded()
        {
            var building = IntegrationTestBuilder.Create()
                .WithSchedulingStrategy(new DirectionAwareStrategy())
                .WithLogger(new MockLogger())
                .WithMetrics(new SystemMetrics())
                .Build();

            var firstResult = building.RequestPassengerJourney(sourceFloor: 0, destinationFloor: 5);
            Assert.True(firstResult.IsSuccess);
            
            building.ProcessTick();
            
            var secondResult = building.RequestPassengerJourney(sourceFloor: 0, destinationFloor: 7);
            Assert.True(secondResult.IsSuccess);
            
            var status = building.GetStatus();
            var assignedElevator = status.Elevators.FirstOrDefault(e => 
                e.AssignedHallCallIds.Any());
            
            Assert.NotNull(assignedElevator);
            Assert.Contains(0, assignedElevator.Destinations);
            Assert.Contains(5, assignedElevator.Destinations);
            Assert.Contains(7, assignedElevator.Destinations);
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Priority", "P1")]
        public void RequestPassengerJourney_ElevatorAlreadyAtFloor_AddsDestinationOnly()
        {
            var building = IntegrationTestBuilder.Create()
                .WithSchedulingStrategy(new DirectionAwareStrategy())
                .WithLogger(new MockLogger())
                .WithMetrics(new SystemMetrics())
                .Build();

            var firstResult = building.RequestPassengerJourney(sourceFloor: 0, destinationFloor: 3);
            Assert.True(firstResult.IsSuccess);
            
            TickSimulator.ProcessTicksUntilElevatorReachesFloor(building, 1, 0, maxTicks: 20);
            
            var secondResult = building.RequestPassengerJourney(sourceFloor: 0, destinationFloor: 5);
            Assert.True(secondResult.IsSuccess);
            
            var status = building.GetStatus();
            var elevator = status.Elevators.First(e => e.Id == 1);
            
            var hallCallFloorCount = elevator.Destinations.Count(d => d == 0);
            Assert.True(hallCallFloorCount <= 1);
            Assert.Contains(5, elevator.Destinations);
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Priority", "P1")]
        public void ProcessTick_ElevatorArrivesAtHallCallFloor_CompletesHallCall()
        {
            var building = IntegrationTestBuilder.Create()
                .WithSchedulingStrategy(new DirectionAwareStrategy())
                .WithLogger(new MockLogger())
                .WithMetrics(new SystemMetrics())
                .Build();

            var result = building.RequestHallCall(floor: 5, direction: Direction.UP);
            Assert.True(result.IsSuccess);
            var hallCallId = result.Value!.Id;
            
            building.ProcessTick();
            
            var status = building.GetStatus();
            var assignedElevator = status.Elevators.FirstOrDefault(e => 
                e.AssignedHallCallIds.Contains(hallCallId));
            
            Assert.NotNull(assignedElevator);
            
            TickSimulator.ProcessTicksUntilElevatorReachesFloor(building, assignedElevator.Id, 5, maxTicks: 30);
            
            var finalStatus = building.GetStatus();
            var finalElevator = finalStatus.Elevators.First(e => e.Id == assignedElevator.Id);
            Assert.DoesNotContain(hallCallId, finalElevator.AssignedHallCallIds);
            Assert.Equal(0, finalStatus.PendingHallCallsCount);
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Priority", "P1")]
        public void ProcessTick_MultipleHallCallsAtSameFloor_CompletesMatchingDirection()
        {
            var building = IntegrationTestBuilder.Create()
                .WithSchedulingStrategy(new DirectionAwareStrategy())
                .WithLogger(new MockLogger())
                .WithMetrics(new SystemMetrics())
                .Build();

            var upResult = building.RequestHallCall(floor: 5, direction: Direction.UP);
            var downResult = building.RequestHallCall(floor: 5, direction: Direction.DOWN);
            
            Assert.True(upResult.IsSuccess);
            Assert.True(downResult.IsSuccess);
            
            building.ProcessTick();
            
            var status = building.GetStatus();
            var upElevator = status.Elevators.FirstOrDefault(e => 
                e.AssignedHallCallIds.Contains(upResult.Value!.Id));
            var downElevator = status.Elevators.FirstOrDefault(e => 
                e.AssignedHallCallIds.Contains(downResult.Value!.Id));
            
            Assert.NotNull(upElevator);
            Assert.NotNull(downElevator);
            
            TickSimulator.ProcessTicksUntilElevatorReachesFloor(building, upElevator.Id, 5, maxTicks: 30);
            
            var finalStatus = building.GetStatus();
            var finalUpElevator = finalStatus.Elevators.First(e => e.Id == upElevator.Id);
            Assert.DoesNotContain(upResult.Value!.Id, finalUpElevator.AssignedHallCallIds);
            Assert.Contains(downResult.Value!.Id, downElevator.AssignedHallCallIds);
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Priority", "P1")]
        public void ProcessTick_HallCallCompleted_RemovesFromElevator()
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
            
            var status = building.GetStatus();
            var assignedElevator = status.Elevators.FirstOrDefault(e => 
                e.AssignedHallCallIds.Contains(hallCallId));
            
            Assert.NotNull(assignedElevator);
            Assert.Contains(hallCallId, assignedElevator.AssignedHallCallIds);
            
            TickSimulator.ProcessTicksUntilElevatorReachesFloor(building, assignedElevator.Id, 3, maxTicks: 30);
            
            TickSimulator.ProcessTicks(building, 5);
            
            var finalStatus = building.GetStatus();
            var finalElevator = finalStatus.Elevators.First(e => e.Id == assignedElevator.Id);
            Assert.DoesNotContain(hallCallId, finalElevator.AssignedHallCallIds);
        }
    }
}
