using System;
using System.Linq;
using Xunit;
using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.ValueObjects;
using ElevatorSystem.Tests.TestHelpers;

namespace ElevatorSystem.Tests.Domain.Entities
{
    /// <summary>
    /// Tests for the Elevator entity.
    /// Covers state machine transitions, movement, door operations, and hall call acceptance logic.
    /// </summary>
    public class ElevatorTests
    {
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P1")]
        public void ProcessTick_IdleWithDestinations_TransitionsToMoving()
        {
            // Arrange
            var elevator = TestBuilders.CreateElevator(id: 1, currentFloor: 0);
            elevator.AddDestination(5);
            
            // Act
            elevator.ProcessTick();
            
            // Assert
            Assert.Equal(ElevatorState.MOVING, elevator.State);
            Assert.Equal(Direction.UP, elevator.Direction);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P1")]
        public void ProcessTick_MovingUp_AdvancesOneFloor()
        {
            // Arrange
            var elevator = TestBuilders.CreateElevator(currentFloor: 0);
            elevator.AddDestination(5);
            elevator.ProcessTick(); // Transition to MOVING
            
            // Act
            elevator.ProcessTick(); // Move one floor up
            
            // Assert
            Assert.Equal(1, elevator.CurrentFloor);
            Assert.Equal(ElevatorState.MOVING, elevator.State);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P1")]
        public void ProcessTick_ArrivesAtDestination_TransitionsToLoading()
        {
            // Arrange
            var elevator = TestBuilders.CreateElevator(currentFloor: 0);
            elevator.AddDestination(1);
            elevator.ProcessTick(); // Tick 1: IDLE → MOVING
            elevator.ProcessTick(); // Tick 2: Move from 0 to 1
            
            // Act
            elevator.ProcessTick(); // Tick 3: Check arrival, transition to LOADING
            
            // Assert
            Assert.Equal(1, elevator.CurrentFloor);
            Assert.Equal(ElevatorState.LOADING, elevator.State);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P1")]
        public void ProcessTick_LoadingDoorTimerExpires_TransitionsToIdle()
        {
            // Arrange
            var elevator = TestBuilders.CreateElevator(currentFloor: 0, doorOpenTicks: 3);
            elevator.AddDestination(1);
            elevator.ProcessTick(); // Tick 1: IDLE → MOVING
            elevator.ProcessTick(); // Tick 2: Move to floor 1
            elevator.ProcessTick(); // Tick 3: Arrive → LOADING (timer = 3)
            
            // Act - Wait for door timer to expire (3 ticks)
            elevator.ProcessTick(); // Tick 4: Timer = 2
            elevator.ProcessTick(); // Tick 5: Timer = 1
            elevator.ProcessTick(); // Tick 6: Timer = 0 → IDLE
            
            // Assert
            Assert.Equal(ElevatorState.IDLE, elevator.State);
            Assert.Equal(Direction.IDLE, elevator.Direction);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P1")]
        public void ProcessTick_LoadingWithMoreDestinations_ContinuesMoving()
        {
            // Arrange
            var elevator = TestBuilders.CreateElevator(currentFloor: 0, doorOpenTicks: 1);
            elevator.AddDestination(1);
            elevator.AddDestination(5);
            elevator.ProcessTick(); // Tick 1: IDLE → MOVING
            elevator.ProcessTick(); // Tick 2: Move to 1
            elevator.ProcessTick(); // Tick 3: Arrive → LOADING (timer = 1)
            
            // Act
            elevator.ProcessTick(); // Tick 4: Timer = 0 → MOVING (continue to floor 5)
            
            // Assert
            Assert.Equal(ElevatorState.MOVING, elevator.State);
            Assert.Equal(Direction.UP, elevator.Direction);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P1")]
        public void ProcessTick_StuckInLoading_SafetyTimeoutForcesTransition()
        {
            // Arrange
            var elevator = TestBuilders.CreateElevator(currentFloor: 0, doorOpenTicks: 100); // Very long timer
            elevator.AddDestination(1);
            elevator.ProcessTick(); // Tick 1: IDLE → MOVING
            elevator.ProcessTick(); // Tick 2: Move to floor 1
            elevator.ProcessTick(); // Tick 3: Arrive → LOADING
            
            // Act - Simulate 11 more ticks (safety timeout is 10)
            for (int i = 0; i < 11; i++)
            {
                elevator.ProcessTick();
            }
            
            // Assert
            Assert.Equal(ElevatorState.IDLE, elevator.State); // Forced transition
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P1")]
        public void CanAcceptHallCall_IdleElevator_ReturnsTrue()
        {
            // Arrange
            var elevator = TestBuilders.CreateElevator(currentFloor: 0);
            var hallCall = new HallCall(7, Direction.UP);
            
            // Act
            var canAccept = elevator.CanAcceptHallCall(hallCall);
            
            // Assert
            Assert.True(canAccept);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P1")]
        public void CanAcceptHallCall_SameDirectionBetweenCurrentAndFurthest_ReturnsTrue()
        {
            // Arrange
            var elevator = TestBuilders.CreateElevator(currentFloor: 0);
            elevator.AddDestination(8);
            elevator.ProcessTick(); // IDLE → MOVING UP
            elevator.ProcessTick(); // Move to floor 1
            elevator.ProcessTick(); // Move to floor 2
            var hallCall = new HallCall(5, Direction.UP); // Between 2 and 8
            
            // Act
            var canAccept = elevator.CanAcceptHallCall(hallCall);
            
            // Assert
            Assert.True(canAccept);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P1")]
        public void CanAcceptHallCall_OppositeDirection_ReturnsFalse()
        {
            // Arrange
            var elevator = TestBuilders.CreateElevator(currentFloor: 0);
            elevator.AddDestination(8);
            elevator.ProcessTick(); // IDLE → MOVING UP
            var hallCall = new HallCall(3, Direction.DOWN); // Opposite direction
            
            // Act
            var canAccept = elevator.CanAcceptHallCall(hallCall);
            
            // Assert
            Assert.False(canAccept);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P1")]
        public void CanAcceptHallCall_AtCurrentFloorInLoading_ReturnsFalse()
        {
            // Arrange
            var elevator = TestBuilders.CreateElevator(currentFloor: 0);
            elevator.AddDestination(5);
            elevator.ProcessTick(); // IDLE → MOVING
            for (int i = 0; i < 5; i++)
            {
                elevator.ProcessTick(); // Move to floor 5
            }
            // Now at floor 5 in LOADING state
            var hallCall = new HallCall(5, Direction.UP);
            
            // Act
            var canAccept = elevator.CanAcceptHallCall(hallCall);
            
            // Assert
            Assert.False(canAccept); // Already servicing this floor
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P1")]
        public void AssignHallCall_AddsDestinationAndHallCallId()
        {
            // Arrange
            var elevator = TestBuilders.CreateElevator();
            var hallCall = new HallCall(7, Direction.UP);
            
            // Act
            elevator.AssignHallCall(hallCall);
            
            // Assert
            var status = elevator.GetStatus();
            Assert.Contains(7, status.Destinations);
            Assert.Contains(hallCall.Id, status.AssignedHallCallIds);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P1")]
        public void RemoveHallCallId_RemovesFromAssignedList()
        {
            // Arrange
            var elevator = TestBuilders.CreateElevator();
            var hallCall = new HallCall(7, Direction.UP);
            elevator.AssignHallCall(hallCall);
            
            // Act
            elevator.RemoveHallCallId(hallCall.Id);
            
            // Assert
            var status = elevator.GetStatus();
            Assert.DoesNotContain(hallCall.Id, status.AssignedHallCallIds);
        }
    }
}
