using ElevatorSystem.Common;
using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.Services;
using ElevatorSystem.Domain.ValueObjects;
using ElevatorSystem.Infrastructure.Metrics;
using ElevatorSystem.Tests.Integration.TestHelpers;
using ElevatorSystem.Tests.TestHelpers;

namespace ElevatorSystem.Tests.Integration
{
    /// <summary>
    /// Integration tests for multiple elevators coordinating together.
    /// </summary>
    public class MultiElevatorCoordinationIntegrationTests
    {
        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Priority", "P1")]
        public void MultipleElevators_IndependentMovement_NoInterference()
        {
            var building = IntegrationTestBuilder.Create()
                .WithSchedulingStrategy(new DirectionAwareStrategy())
                .WithLogger(new MockLogger())
                .WithMetrics(new SystemMetrics())
                .Build();

            var firstResult = building.RequestHallCall(floor: 0, direction: Direction.UP);
            var secondResult = building.RequestHallCall(floor: 8, direction: Direction.DOWN);
            
            Assert.True(firstResult.IsSuccess);
            Assert.True(secondResult.IsSuccess);
            
            building.ProcessTick();
            
            var status = building.GetStatus();
            var firstElevator = status.Elevators.FirstOrDefault(e => 
                e.AssignedHallCallIds.Contains(firstResult.Value!.Id));
            var secondElevator = status.Elevators.FirstOrDefault(e => 
                e.AssignedHallCallIds.Contains(secondResult.Value!.Id));
            
            Assert.NotNull(firstElevator);
            Assert.NotNull(secondElevator);
            Assert.NotEqual(firstElevator.Id, secondElevator.Id);
            
            TickSimulator.ProcessTicks(building, 5);
            
            var finalStatus = building.GetStatus();
            var finalFirst = finalStatus.Elevators.First(e => e.Id == firstElevator.Id);
            var finalSecond = finalStatus.Elevators.First(e => e.Id == secondElevator.Id);
            
            Assert.NotEqual(finalFirst.CurrentFloor, finalSecond.CurrentFloor);
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Priority", "P1")]
        public void MultipleElevators_AssignToDifferentElevators_NoConflict()
        {
            var building = IntegrationTestBuilder.Create()
                .WithSchedulingStrategy(new DirectionAwareStrategy())
                .WithLogger(new MockLogger())
                .WithMetrics(new SystemMetrics())
                .Build();

            var firstResult = building.RequestHallCall(floor: 2, direction: Direction.UP);
            var secondResult = building.RequestHallCall(floor: 7, direction: Direction.DOWN);
            
            Assert.True(firstResult.IsSuccess);
            Assert.True(secondResult.IsSuccess);
            
            building.ProcessTick();
            
            var status = building.GetStatus();
            var firstElevator = status.Elevators.FirstOrDefault(e => 
                e.AssignedHallCallIds.Contains(firstResult.Value!.Id));
            var secondElevator = status.Elevators.FirstOrDefault(e => 
                e.AssignedHallCallIds.Contains(secondResult.Value!.Id));
            
            Assert.NotNull(firstElevator);
            Assert.NotNull(secondElevator);
            Assert.NotEqual(firstElevator.Id, secondElevator.Id);
            Assert.Contains(2, firstElevator.Destinations);
            Assert.Contains(7, secondElevator.Destinations);
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Priority", "P1")]
        public void MultipleElevators_OneIdleOneMoving_LoadBalancing()
        {
            var building = IntegrationTestBuilder.Create()
                .WithSchedulingStrategy(new DirectionAwareStrategy())
                .WithLogger(new MockLogger())
                .WithMetrics(new SystemMetrics())
                .Build();

            var firstResult = building.RequestHallCall(floor: 0, direction: Direction.UP);
            Assert.True(firstResult.IsSuccess);
            
            building.ProcessTick();
            building.ProcessTick();
            
            var status = building.GetStatus();
            var movingElevator = status.Elevators.FirstOrDefault(e => e.State == ElevatorState.MOVING);
            var idleElevator = status.Elevators.FirstOrDefault(e => e.State == ElevatorState.IDLE);
            
            Assert.NotNull(movingElevator);
            Assert.NotNull(idleElevator);
            
            var secondResult = building.RequestHallCall(floor: 5, direction: Direction.UP);
            Assert.True(secondResult.IsSuccess);
            
            building.ProcessTick();
            
            var finalStatus = building.GetStatus();
            var assignedElevator = finalStatus.Elevators.FirstOrDefault(e => 
                e.AssignedHallCallIds.Contains(secondResult.Value!.Id));
            
            Assert.NotNull(assignedElevator);
            Assert.Equal(idleElevator.Id, assignedElevator.Id);
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Priority", "P1")]
        public void MultipleElevators_AllMoving_SelectsBestAvailable()
        {
            var building = IntegrationTestBuilder.Create()
                .WithSchedulingStrategy(new DirectionAwareStrategy())
                .WithLogger(new MockLogger())
                .WithMetrics(new SystemMetrics())
                .Build();

            var firstResult = building.RequestHallCall(floor: 0, direction: Direction.UP);
            var secondResult = building.RequestHallCall(floor: 2, direction: Direction.UP);
            var thirdResult = building.RequestHallCall(floor: 4, direction: Direction.UP);
            
            building.ProcessTick();
            building.ProcessTick();
            
            var fourthResult = building.RequestHallCall(floor: 6, direction: Direction.UP);
            Assert.True(fourthResult.IsSuccess);
            
            building.ProcessTick();
            
            var status = building.GetStatus();
            var assignedElevator = status.Elevators.FirstOrDefault(e => 
                e.AssignedHallCallIds.Contains(fourthResult.Value!.Id));
            
            Assert.NotNull(assignedElevator);
            Assert.True(assignedElevator.State == ElevatorState.MOVING || assignedElevator.State == ElevatorState.LOADING);
            Assert.Equal(Direction.UP, assignedElevator.Direction);
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Priority", "P1")]
        public void MultipleElevators_QueueDistribution_EvenlyDistributed()
        {
            var building = IntegrationTestBuilder.Create()
                .WithSchedulingStrategy(new DirectionAwareStrategy())
                .WithLogger(new MockLogger())
                .WithMetrics(new SystemMetrics())
                .Build();

            var results = new List<Result<HallCall>>();
            for (int i = 0; i < 4; i++)
            {
                var result = building.RequestHallCall(floor: i * 2, direction: Direction.UP);
                results.Add(result);
            }
            
            building.ProcessTick();
            
            var status = building.GetStatus();
            var elevatorsWithAssignments = status.Elevators
                .Where(e => e.AssignedHallCallIds.Any())
                .ToList();
            
            Assert.True(elevatorsWithAssignments.Count >= 2);
            
            var allAssignedHallCallIds = elevatorsWithAssignments
                .SelectMany(e => e.AssignedHallCallIds)
                .ToList();
            
            Assert.Equal(4, allAssignedHallCallIds.Count);
        }
    }
}
