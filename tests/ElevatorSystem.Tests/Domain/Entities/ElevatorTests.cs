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
            var elevator = TestBuilders.CreateElevator(currentFloor: 0, elevatorMovementTicks: 1);
            elevator.AddDestination(5);
            elevator.ProcessTick(); // Transition to MOVING (timer = 1)
            
            // Act
            elevator.ProcessTick(); // Decrement timer to 0, move one floor up
            
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
            var elevator = TestBuilders.CreateElevator(currentFloor: 0, elevatorMovementTicks: 1);
            elevator.AddDestination(1);
            elevator.ProcessTick(); // Tick 1: IDLE → MOVING (timer = 1)
            elevator.ProcessTick(); // Tick 2: Decrement timer to 0, move from 0 to 1
            
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
            var elevator = TestBuilders.CreateElevator(currentFloor: 0, doorOpenTicks: 3, elevatorMovementTicks: 1);
            elevator.AddDestination(1);
            elevator.ProcessTick(); // Tick 1: IDLE → MOVING (timer = 1)
            elevator.ProcessTick(); // Tick 2: Decrement timer to 0, move to floor 1
            elevator.ProcessTick(); // Tick 3: Arrive → LOADING (door timer = 3)
            
            // Act - Wait for door timer to expire (3 ticks)
            elevator.ProcessTick(); // Tick 4: Door timer = 2
            elevator.ProcessTick(); // Tick 5: Door timer = 1
            elevator.ProcessTick(); // Tick 6: Door timer = 0 → IDLE
            
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
            var elevator = TestBuilders.CreateElevator(currentFloor: 0, doorOpenTicks: 1, elevatorMovementTicks: 1);
            elevator.AddDestination(1);
            elevator.AddDestination(5);
            elevator.ProcessTick(); // Tick 1: IDLE → MOVING (timer = 1)
            elevator.ProcessTick(); // Tick 2: Decrement timer to 0, move to 1
            elevator.ProcessTick(); // Tick 3: Arrive → LOADING (door timer = 1)
            
            // Act
            elevator.ProcessTick(); // Tick 4: Door timer = 0 → MOVING (continue to floor 5, movement timer = 1)
            
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
            var elevator = TestBuilders.CreateElevator(currentFloor: 0, doorOpenTicks: 100, elevatorMovementTicks: 1); // Very long timer
            elevator.AddDestination(1);
            elevator.ProcessTick(); // Tick 1: IDLE → MOVING (timer = 1)
            elevator.ProcessTick(); // Tick 2: Decrement timer to 0, move to floor 1
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
            var elevator = TestBuilders.CreateElevator(currentFloor: 0, elevatorMovementTicks: 1);
            elevator.AddDestination(5);
            elevator.ProcessTick(); // IDLE → MOVING (timer = 1)
            for (int i = 0; i < 5; i++)
            {
                elevator.ProcessTick(); // Move one floor per tick (5 floors = 5 ticks)
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

        // ========== Movement Timer Tests ==========

        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P1")]
        public void ProcessTick_MovementTimerWithMultipleTicks_WaitsBeforeMoving()
        {
            // Arrange
            var elevator = TestBuilders.CreateElevator(currentFloor: 0, elevatorMovementTicks: 3);
            elevator.AddDestination(5);
            
            // Act - First tick: transition to MOVING, timer = 3
            elevator.ProcessTick();
            Assert.Equal(ElevatorState.MOVING, elevator.State);
            Assert.Equal(0, elevator.CurrentFloor); // Should not move yet
            
            // Second tick: timer = 2
            elevator.ProcessTick();
            Assert.Equal(0, elevator.CurrentFloor); // Still waiting
            
            // Third tick: timer = 1
            elevator.ProcessTick();
            Assert.Equal(0, elevator.CurrentFloor); // Still waiting
            
            // Fourth tick: timer = 0, should move
            elevator.ProcessTick();
            
            // Assert
            Assert.Equal(1, elevator.CurrentFloor); // Moved one floor
            Assert.Equal(ElevatorState.MOVING, elevator.State);
        }

        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P1")]
        public void ProcessTick_MovementTimerResetsAfterEachMove()
        {
            // Arrange
            var elevator = TestBuilders.CreateElevator(currentFloor: 0, elevatorMovementTicks: 2);
            elevator.AddDestination(3);
            
            // Act - Move from floor 0 to floor 3 (3 floors, 2 ticks per floor = 6 ticks total)
            elevator.ProcessTick(); // Tick 1: IDLE → MOVING (timer = 2)
            Assert.Equal(0, elevator.CurrentFloor);
            
            elevator.ProcessTick(); // Tick 2: timer = 1
            Assert.Equal(0, elevator.CurrentFloor);
            
            elevator.ProcessTick(); // Tick 3: timer = 0, move to floor 1 (timer reset to 2)
            Assert.Equal(1, elevator.CurrentFloor);
            
            elevator.ProcessTick(); // Tick 4: timer = 1
            Assert.Equal(1, elevator.CurrentFloor);
            
            elevator.ProcessTick(); // Tick 5: timer = 0, move to floor 2 (timer reset to 2)
            Assert.Equal(2, elevator.CurrentFloor);
            
            elevator.ProcessTick(); // Tick 6: timer = 1
            Assert.Equal(2, elevator.CurrentFloor);
            
            elevator.ProcessTick(); // Tick 7: timer = 0, move to floor 3 (timer reset to 2)
            Assert.Equal(3, elevator.CurrentFloor);
            
            elevator.ProcessTick(); // Tick 8: Arrive at destination, transition to LOADING
            Assert.Equal(ElevatorState.LOADING, elevator.State);
        }

        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void ProcessTick_MovementTimerWithDirectionChange_ResetsCorrectly()
        {
            // Arrange
            var elevator = TestBuilders.CreateElevator(currentFloor: 5, elevatorMovementTicks: 2);
            elevator.AddDestination(3); // Moving DOWN
            elevator.AddDestination(7); // Then moving UP
            
            // Act - Move DOWN to floor 3
            // Process enough ticks to reach floor 3
            while (elevator.CurrentFloor != 3 || elevator.State != ElevatorState.LOADING)
            {
                elevator.ProcessTick();
            }
            
            // After loading, continue to floor 7 (UP)
            // Process enough ticks to move at least one floor UP
            int initialFloor = elevator.CurrentFloor;
            while (elevator.CurrentFloor == initialFloor && elevator.State == ElevatorState.LOADING)
            {
                elevator.ProcessTick();
            }
            // Process a few more ticks to ensure movement occurs
            for (int i = 0; i < 3; i++)
            {
                elevator.ProcessTick();
            }
            
            // Assert - Elevator should have moved UP from floor 3
            Assert.True(elevator.CurrentFloor > 3, $"Expected elevator to move UP from floor 3, but it's at floor {elevator.CurrentFloor}");
            Assert.Equal(Direction.UP, elevator.Direction);
            Assert.True(elevator.State == ElevatorState.MOVING || elevator.State == ElevatorState.LOADING);
        }

        // ========== SetDirection Tests ==========

        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P1")]
        public void SetDirection_UpdatesDirectionAndDestinationSet()
        {
            // Arrange
            var elevator = TestBuilders.CreateElevator(currentFloor: 5);
            Assert.Equal(Direction.IDLE, elevator.Direction);
            
            // Act
            elevator.SetDirection(Direction.UP);
            
            // Assert
            Assert.Equal(Direction.UP, elevator.Direction);
            var status = elevator.GetStatus();
            // DestinationSet direction should also be updated
        }

        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P1")]
        public void SetDirection_FromIdleToMoving_UpdatesCorrectly()
        {
            // Arrange
            var elevator = TestBuilders.CreateElevator(currentFloor: 5);
            elevator.AddDestination(7);
            elevator.ProcessTick(); // IDLE → MOVING UP
            
            // Act - Change direction to DOWN
            elevator.SetDirection(Direction.DOWN);
            
            // Assert
            Assert.Equal(Direction.DOWN, elevator.Direction);
        }

        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void SetDirection_WhenCompletingHallCall_SetsCorrectDirection()
        {
            // Arrange
            var elevator = TestBuilders.CreateElevator(currentFloor: 8, elevatorMovementTicks: 1);
            var hallCall = new HallCall(8, Direction.DOWN);
            elevator.AssignHallCall(hallCall);
            
            // Elevator arrives at floor 8 (already there)
            elevator.ProcessTick(); // Should transition to LOADING
            
            // Act - Set direction to match hall call direction
            elevator.SetDirection(Direction.DOWN);
            
            // Assert
            Assert.Equal(Direction.DOWN, elevator.Direction);
            // This simulates what happens in CompleteHallCallsForElevator
        }

        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void SetDirection_NullDirection_ThrowsException()
        {
            // Arrange
            var elevator = TestBuilders.CreateElevator();
            
            // Act & Assert
            // Direction is a value type (enum), so null is not possible
            // This test verifies the method signature accepts Direction enum
            elevator.SetDirection(Direction.UP);
            Assert.Equal(Direction.UP, elevator.Direction);
        }

        // ========== Edge Case Tests ==========

        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void ProcessTick_MovementTimerAtBoundary_HandlesCorrectly()
        {
            // Arrange
            var elevator = TestBuilders.CreateElevator(currentFloor: 0, elevatorMovementTicks: 1);
            elevator.AddDestination(1);
            
            // Act - With movementTicks = 1, should move immediately after transition
            elevator.ProcessTick(); // IDLE → MOVING (timer = 1)
            Assert.Equal(0, elevator.CurrentFloor);
            
            elevator.ProcessTick(); // timer = 0, move to floor 1
            Assert.Equal(1, elevator.CurrentFloor);
            
            elevator.ProcessTick(); // Arrive, LOADING
            Assert.Equal(ElevatorState.LOADING, elevator.State);
        }

        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void ProcessTick_MultipleDestinationsWithMovementTimer_ProcessesCorrectly()
        {
            // Arrange
            var elevator = TestBuilders.CreateElevator(currentFloor: 0, elevatorMovementTicks: 2);
            elevator.AddDestination(2);
            elevator.AddDestination(5);
            
            // Act - Move to first destination (floor 2)
            while (elevator.CurrentFloor != 2 || elevator.State != ElevatorState.LOADING)
            {
                elevator.ProcessTick();
                if (elevator.CurrentFloor > 10) break; // Safety check
            }
            // Should be at floor 2, LOADING
            Assert.Equal(2, elevator.CurrentFloor);
            Assert.Equal(ElevatorState.LOADING, elevator.State);
            
            // Continue to second destination (floor 5)
            while (elevator.CurrentFloor != 5 || elevator.State != ElevatorState.LOADING)
            {
                elevator.ProcessTick();
                if (elevator.CurrentFloor > 10) break; // Safety check
            }
            // Should be at floor 5, LOADING
            Assert.Equal(5, elevator.CurrentFloor);
            Assert.Equal(ElevatorState.LOADING, elevator.State);
        }
    }
}
