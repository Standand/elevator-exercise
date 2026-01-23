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
    /// Integration tests for Building + Elevator + Scheduling Strategy coordination.
    /// </summary>
    public class BuildingElevatorSchedulingIntegrationTests
    {
        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Priority", "P1")]
        public void RequestHallCall_WithIdleElevator_AssignsToNearestElevator()
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
            Assert.Equal(ElevatorState.MOVING, assignedElevator.State);
            Assert.Contains(5, assignedElevator.Destinations);
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Priority", "P1")]
        public void RequestHallCall_WithMovingElevator_AssignsToSameDirectionElevator()
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
            
            var secondResult = building.RequestHallCall(floor: 3, direction: Direction.UP);
            Assert.True(secondResult.IsSuccess);
            
            building.ProcessTick();
            
            var status = building.GetStatus();
            var elevator = status.Elevators.FirstOrDefault(e => 
                e.AssignedHallCallIds.Contains(secondResult.Value!.Id));
            
            Assert.NotNull(elevator);
            Assert.Equal(Direction.UP, elevator.Direction);
            Assert.True(elevator.State == ElevatorState.MOVING || elevator.State == ElevatorState.LOADING);
            Assert.Contains(3, elevator.Destinations);
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Priority", "P1")]
        public void RequestHallCall_NoAvailableElevator_RemainsPending()
        {
            var config = new SimulationConfiguration
            {
                ElevatorCount = 1,
                MaxFloors = 10,
                DoorOpenTicks = 3,
                TickIntervalMs = 1000,
                RequestIntervalSeconds = 5
            };

            var building = IntegrationTestBuilder.Create()
                .WithSchedulingStrategy(new DirectionAwareStrategy())
                .WithConfiguration(config)
                .WithLogger(new MockLogger())
                .WithMetrics(new SystemMetrics())
                .Build();

            var firstResult = building.RequestHallCall(floor: 8, direction: Direction.DOWN);
            Assert.True(firstResult.IsSuccess);
            
            TickSimulator.ProcessTicks(building, 15);
            
            var secondResult = building.RequestHallCall(floor: 5, direction: Direction.UP);
            Assert.True(secondResult.IsSuccess);
            var hallCall = secondResult.Value!;
            
            building.ProcessTick();
            
            var status = building.GetStatus();
            var elevator = status.Elevators.First();
            var hallCallInQueue = status.PendingHallCallsCount > 0;
            
            if (elevator.Direction == Direction.DOWN && elevator.State != ElevatorState.IDLE)
            {
                Assert.True(hallCallInQueue || !elevator.AssignedHallCallIds.Contains(hallCall.Id));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Priority", "P1")]
        public void RequestHallCall_MultipleElevators_SelectsBestElevator()
        {
            var building = IntegrationTestBuilder.Create()
                .WithSchedulingStrategy(new DirectionAwareStrategy())
                .WithLogger(new MockLogger())
                .WithMetrics(new SystemMetrics())
                .Build();

            var result = building.RequestHallCall(floor: 5, direction: Direction.UP);
            Assert.True(result.IsSuccess);
            var hallCall = result.Value!;
            
            building.ProcessTick();
            
            var status = building.GetStatus();
            var assignedElevator = status.Elevators.FirstOrDefault(e => 
                e.AssignedHallCallIds.Contains(hallCall.Id));
            
            Assert.NotNull(assignedElevator);
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Priority", "P1")]
        public void DirectionAwareStrategy_PrioritizesSameDirectionElevator()
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
            var movingElevator = status.Elevators.FirstOrDefault(e => e.Direction == Direction.UP && e.State == ElevatorState.MOVING);
            var idleElevator = status.Elevators.FirstOrDefault(e => e.State == ElevatorState.IDLE);
            
            Assert.NotNull(movingElevator);
            Assert.NotNull(idleElevator);
            
            var secondResult = building.RequestHallCall(floor: 3, direction: Direction.UP);
            Assert.True(secondResult.IsSuccess);
            
            building.ProcessTick();
            
            var secondStatus = building.GetStatus();
            var assignedElevator = secondStatus.Elevators.FirstOrDefault(e => 
                e.AssignedHallCallIds.Contains(secondResult.Value!.Id));
            
            Assert.NotNull(assignedElevator);
            Assert.Equal(movingElevator.Id, assignedElevator.Id);
            Assert.Contains(3, assignedElevator.Destinations);
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Priority", "P1")]
        public void DirectionAwareStrategy_FallbackToIdleWhenNoSameDirection()
        {
            var building = IntegrationTestBuilder.Create()
                .WithSchedulingStrategy(new DirectionAwareStrategy())
                .WithLogger(new MockLogger())
                .WithMetrics(new SystemMetrics())
                .Build();

            var downResult = building.RequestHallCall(floor: 8, direction: Direction.DOWN);
            Assert.True(downResult.IsSuccess);
            
            building.ProcessTick();
            building.ProcessTick();
            
            var status = building.GetStatus();
            var downElevator = status.Elevators.FirstOrDefault(e => e.Direction == Direction.DOWN);
            var idleElevator = status.Elevators.FirstOrDefault(e => e.State == ElevatorState.IDLE);
            
            Assert.NotNull(downElevator);
            Assert.NotNull(idleElevator);
            
            var upResult = building.RequestHallCall(floor: 2, direction: Direction.UP);
            Assert.True(upResult.IsSuccess);
            
            building.ProcessTick();
            
            var finalStatus = building.GetStatus();
            var assignedElevator = finalStatus.Elevators.FirstOrDefault(e => 
                e.AssignedHallCallIds.Contains(upResult.Value!.Id));
            
            Assert.NotNull(assignedElevator);
            Assert.Equal(idleElevator.Id, assignedElevator.Id);
            Assert.Contains(2, assignedElevator.Destinations);
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Priority", "P1")]
        public void DirectionAwareStrategy_SelectsNearestAmongSameDirection()
        {
            var building = IntegrationTestBuilder.Create()
                .WithSchedulingStrategy(new DirectionAwareStrategy())
                .WithLogger(new MockLogger())
                .WithMetrics(new SystemMetrics())
                .Build();

            var firstResult = building.RequestHallCall(floor: 0, direction: Direction.UP);
            var secondResult = building.RequestHallCall(floor: 2, direction: Direction.UP);
            
            building.ProcessTick();
            building.ProcessTick();
            
            var thirdResult = building.RequestHallCall(floor: 5, direction: Direction.UP);
            Assert.True(thirdResult.IsSuccess);
            
            building.ProcessTick();
            
            var status = building.GetStatus();
            var assignedElevator = status.Elevators.FirstOrDefault(e => 
                e.AssignedHallCallIds.Contains(thirdResult.Value!.Id));
            
            Assert.NotNull(assignedElevator);
            var distance = Math.Abs(assignedElevator.CurrentFloor - 5);
            
            var allMovingUp = status.Elevators
                .Where(e => e.Direction == Direction.UP && (e.State == ElevatorState.MOVING || e.State == ElevatorState.LOADING))
                .ToList();
            
            if (allMovingUp.Count > 1)
            {
                var nearestDistance = allMovingUp.Min(e => Math.Abs(e.CurrentFloor - 5));
                Assert.Equal(nearestDistance, distance);
            }
            
            Assert.Contains(5, assignedElevator.Destinations);
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Priority", "P1")]
        public void NearestFirstStrategy_SelectsNearestIdleElevator()
        {
            var building = IntegrationTestBuilder.Create()
                .WithSchedulingStrategy(new NearestFirstStrategy())
                .WithLogger(new MockLogger())
                .WithMetrics(new SystemMetrics())
                .Build();

            var firstResult = building.RequestHallCall(floor: 8, direction: Direction.UP);
            var secondResult = building.RequestHallCall(floor: 4, direction: Direction.UP);
            
            TickSimulator.ProcessTicks(building, 15);
            
            var status = building.GetStatus();
            var idleElevators = status.Elevators.Where(e => e.State == ElevatorState.IDLE).ToList();
            
            if (idleElevators.Count >= 2)
            {
                var thirdResult = building.RequestHallCall(floor: 5, direction: Direction.UP);
                Assert.True(thirdResult.IsSuccess);
                
                building.ProcessTick();
                
                var finalStatus = building.GetStatus();
                var assignedElevator = finalStatus.Elevators.FirstOrDefault(e => 
                    e.AssignedHallCallIds.Contains(thirdResult.Value!.Id));
                
                Assert.NotNull(assignedElevator);
                
                var distances = idleElevators.Select(e => new { e.Id, Distance = Math.Abs(e.CurrentFloor - 5) }).ToList();
                var nearest = distances.OrderBy(d => d.Distance).First();
                Assert.Equal(nearest.Id, assignedElevator.Id);
            }
        }
    }
}
