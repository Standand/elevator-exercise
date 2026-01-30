using System;
using System.Collections.Generic;
using Xunit;
using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.Services;
using ElevatorSystem.Domain.ValueObjects;
using ElevatorSystem.Tests.TestHelpers;

namespace ElevatorSystem.Tests.Domain.Services
{
    /// <summary>
    /// Tests for DirectionAwareStrategy scheduling algorithm.
    /// Validates time-based cost calculation, load consideration, and timeout-based fallback.
    /// </summary>
    public class DirectionAwareStrategyTests
    {
        // Helper to create a HallCall with a specific age (for timeout testing)
        private static HallCall CreateHallCallWithAge(int floor, Direction direction, TimeSpan age)
        {
            var createdAt = DateTime.UtcNow - age;
            return new HallCall(floor, direction, createdAt);
        }

        // Helper to move elevator to a specific floor and make it IDLE
        private static void MoveElevatorToFloorAndIdle(Elevator elevator, int targetFloor, int movementTicks = 1, int doorOpenTicks = 3)
        {
            while (elevator.CurrentFloor < targetFloor)
            {
                if (elevator.State == ElevatorState.IDLE && elevator.GetDestinationCount() == 0)
                {
                    elevator.AddDestination(targetFloor);
                }
                elevator.ProcessTick();
            }
            // Wait for door to close and become IDLE
            while (elevator.State != ElevatorState.IDLE)
            {
                elevator.ProcessTick();
            }
        }

        // ========== Basic Selection Tests ==========

        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P1")]
        public void SelectBestElevator_PrioritizesSameDirection()
        {
            // Test: When both IDLE and MOVING elevators are available, time-based cost determines the winner
            // In this case, MOVING elevator is closer, so it should win despite load penalty
            
            // Arrange
            var strategy = new DirectionAwareStrategy();
            var hallCall = new HallCall(5, Direction.UP);
            
            // IDLE elevator at floor 0 (far from hall call at floor 5)
            var elevator1 = TestBuilders.CreateElevator(id: 1, currentFloor: 0);
            
            // MOVING elevator at floor 3, going to floor 8 (hall call at 5 is on route, very close)
            var elevator2 = TestBuilders.CreateElevator(id: 2, currentFloor: 0);
            elevator2.AddDestination(8);
            elevator2.ProcessTick(); // IDLE → MOVING
            for (int i = 0; i < 3; i++) elevator2.ProcessTick(); // Move to floor 3
            
            var elevators = new List<Elevator> { elevator1, elevator2 };
            
            // Act
            var selected = strategy.SelectBestElevator(hallCall, elevators);
            
            // Assert
            // Elevator 1 (IDLE): distance 5 floors * 1 tick = 5 ticks
            // Elevator 2 (MOVING): distance 2 floors * 1 tick = 2 ticks + load penalty (1 stop * 2 = 2) = 4 ticks
            // Elevator 2 should win (4 < 5)
            Assert.Equal(2, selected!.Id);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P1")]
        public void SelectBestElevator_PicksNearestWhenMultipleSameDirection()
        {
            // Arrange
            var strategy = new DirectionAwareStrategy();
            var hallCall = new HallCall(5, Direction.UP);
            
            var elevator1 = TestBuilders.CreateElevator(id: 1, currentFloor: 0);
            elevator1.AddDestination(8);
            elevator1.ProcessTick(); // Moving UP from 0
            elevator1.ProcessTick(); // Move to 1
            elevator1.ProcessTick(); // Move to 2
            
            var elevator2 = TestBuilders.CreateElevator(id: 2, currentFloor: 0);
            elevator2.AddDestination(8);
            elevator2.ProcessTick(); // Moving UP from 0
            elevator2.ProcessTick(); // Move to 1
            elevator2.ProcessTick(); // Move to 2
            elevator2.ProcessTick(); // Move to 3
            elevator2.ProcessTick(); // Move to 4
            
            var elevators = new List<Elevator> { elevator1, elevator2 };
            
            // Act
            var selected = strategy.SelectBestElevator(hallCall, elevators);
            
            // Assert
            Assert.Equal(2, selected!.Id); // Elevator 2 is nearer (floor 4 vs 2)
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P1")]
        public void SelectBestElevator_FallbackToIdleWhenNoSameDirection()
        {
            // Arrange
            var strategy = new DirectionAwareStrategy();
            var hallCall = new HallCall(5, Direction.UP);
            
            // Create elevator 1 moving DOWN (opposite direction)
            var elevator1 = TestBuilders.CreateElevator(id: 1, currentFloor: 0);
            elevator1.AddDestination(8);
            for (int i = 0; i < 10; i++) elevator1.ProcessTick(); // Move to floor 8
            elevator1.AddDestination(2); // Now going DOWN
            elevator1.ProcessTick(); // Start moving DOWN
            
            // Create idle elevator 2
            var elevator2 = TestBuilders.CreateElevator(id: 2, currentFloor: 0);
            
            var elevators = new List<Elevator> { elevator1, elevator2 };
            
            // Act
            var selected = strategy.SelectBestElevator(hallCall, elevators);
            
            // Assert
            Assert.Equal(2, selected!.Id); // Idle elevator preferred over opposite direction
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P1")]
        public void SelectBestElevator_NoElevatorsAvailable_ReturnsNull()
        {
            // Arrange
            var strategy = new DirectionAwareStrategy();
            var hallCall = new HallCall(5, Direction.UP);
            
            var elevator1 = TestBuilders.CreateElevator(id: 1, currentFloor: 0, elevatorMovementTicks: 1);
            elevator1.AddDestination(5);
            elevator1.ProcessTick(); // IDLE → MOVING (timer = 1)
            // Move 0->1->2->3->4->5 (6 movements = 6 ticks), arrive at 5 (tick 7)
            for (int i = 0; i < 6; i++) elevator1.ProcessTick(); // Move to floor 5 and LOADING
            
            var elevators = new List<Elevator> { elevator1 };
            
            // Act
            var selected = strategy.SelectBestElevator(hallCall, elevators);
            
            // Assert
            Assert.Null(selected); // Cannot accept (already at floor 5 in LOADING)
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P1")]
        public void SelectBestElevator_EmptyList_ReturnsNull()
        {
            // Arrange
            var strategy = new DirectionAwareStrategy();
            var hallCall = new HallCall(5, Direction.UP);
            var elevators = new List<Elevator>();
            
            // Act
            var selected = strategy.SelectBestElevator(hallCall, elevators);
            
            // Assert
            Assert.Null(selected);
        }

        // ========== TC-3.3: Same Direction Moving vs IDLE - Cost Comparison ==========

        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P1")]
        public void SelectBestElevator_TC33_IdleWinsWhenLowerTimeCost()
        {
            // TC-3.3: Same Direction Moving vs IDLE - Cost Comparison
            // Scenario: IDLE elevator at floor 2, MOVING elevator at floor 1 going to floor 8
            // Hall call at floor 5 (UP)
            // Expected: IDLE should win because it has lower time cost (3 floors * 1 tick = 3 ticks)
            // vs MOVING (4 floors * 1 tick + intermediate stops penalty = 4+ ticks)
            
            // Arrange
            var strategy = new DirectionAwareStrategy();
            var hallCall = new HallCall(5, Direction.UP);
            
            // IDLE elevator at floor 2 (closer, no route extension needed)
            var idleElevator = TestBuilders.CreateElevator(id: 1, currentFloor: 0, elevatorMovementTicks: 1, doorOpenTicks: 3);
            MoveElevatorToFloorAndIdle(idleElevator, 2);
            
            // MOVING elevator at floor 1, going to floor 8 (hall call is on route)
            var movingElevator = TestBuilders.CreateElevator(id: 2, currentFloor: 0, elevatorMovementTicks: 1, doorOpenTicks: 3);
            movingElevator.AddDestination(8);
            movingElevator.ProcessTick(); // IDLE → MOVING
            movingElevator.ProcessTick(); // Move to floor 1
            
            var elevators = new List<Elevator> { idleElevator, movingElevator };
            
            // Act
            var selected = strategy.SelectBestElevator(hallCall, elevators);
            
            // Assert
            // IDLE: distance 3 floors = 3 ticks (no intermediate stops, no load penalty)
            // MOVING: distance 4 floors = 4 ticks + 0 intermediate stops = 4 ticks + load penalty (1 stop * 2 = 2) = 6 ticks
            // IDLE should win
            Assert.Equal(1, selected!.Id);
        }

        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P1")]
        public void SelectBestElevator_TC33_MovingWinsWhenLowerTimeCost()
        {
            // TC-3.3 variant: MOVING elevator wins when it has lower time cost
            // Scenario: IDLE elevator at floor 0, MOVING elevator at floor 4 going to floor 8
            // Hall call at floor 5 (UP)
            // Expected: MOVING should win (1 floor * 1 tick = 1 tick) vs IDLE (5 floors * 1 tick = 5 ticks)
            
            // Arrange
            var strategy = new DirectionAwareStrategy();
            var hallCall = new HallCall(5, Direction.UP);
            
            // IDLE elevator at floor 0 (far)
            var idleElevator = TestBuilders.CreateElevator(id: 1, currentFloor: 0, elevatorMovementTicks: 1, doorOpenTicks: 3);
            
            // MOVING elevator at floor 4, going to floor 8 (hall call is on route, very close)
            var movingElevator = TestBuilders.CreateElevator(id: 2, currentFloor: 0, elevatorMovementTicks: 1, doorOpenTicks: 3);
            movingElevator.AddDestination(8);
            movingElevator.ProcessTick(); // IDLE → MOVING
            for (int i = 0; i < 4; i++) movingElevator.ProcessTick(); // Move to floor 4
            
            var elevators = new List<Elevator> { idleElevator, movingElevator };
            
            // Act
            var selected = strategy.SelectBestElevator(hallCall, elevators);
            
            // Assert
            // IDLE: distance 5 floors = 5 ticks
            // MOVING: distance 1 floor = 1 tick + load penalty (1 stop * 2 = 2) = 3 ticks
            // MOVING should win (3 < 5)
            Assert.Equal(2, selected!.Id);
        }

        // ========== Time-Based Cost Calculation Tests ==========

        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P1")]
        public void SelectBestElevator_IdleElevator_UsesDistanceTimesMovementTicks()
        {
            // Test: IDLE elevator cost = distance * movementTicks (no intermediate stops, no loading time)
            
            // Arrange
            var strategy = new DirectionAwareStrategy();
            var hallCall = new HallCall(5, Direction.UP);
            
            // IDLE elevator at floor 2, movementTicks = 2
            var elevator1 = TestBuilders.CreateElevator(id: 1, currentFloor: 0, elevatorMovementTicks: 2, doorOpenTicks: 3);
            MoveElevatorToFloorAndIdle(elevator1, 2);
            
            // IDLE elevator at floor 0, movementTicks = 2
            var elevator2 = TestBuilders.CreateElevator(id: 2, currentFloor: 0, elevatorMovementTicks: 2, doorOpenTicks: 3);
            
            var elevators = new List<Elevator> { elevator1, elevator2 };
            
            // Act
            var selected = strategy.SelectBestElevator(hallCall, elevators);
            
            // Assert
            // Elevator 1: distance 3 floors * 2 ticks = 6 ticks
            // Elevator 2: distance 5 floors * 2 ticks = 10 ticks
            // Elevator 1 should win
            Assert.Equal(1, selected!.Id);
        }

        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P1")]
        public void SelectBestElevator_SameDirectionOnRoute_IncludesIntermediateStops()
        {
            // Test: Same direction elevator on route includes intermediate stops loading time
            
            // Arrange
            var strategy = new DirectionAwareStrategy();
            var hallCall = new HallCall(5, Direction.UP);
            
            // MOVING elevator at floor 1, going to floor 8, with stop at floor 3
            // Hall call at floor 5 is on route
            var elevator1 = TestBuilders.CreateElevator(id: 1, currentFloor: 0, elevatorMovementTicks: 1, doorOpenTicks: 3);
            elevator1.AddDestination(3);
            elevator1.AddDestination(8);
            elevator1.ProcessTick(); // IDLE → MOVING
            elevator1.ProcessTick(); // Move to floor 1
            
            // MOVING elevator at floor 1, going to floor 8, no intermediate stops
            var elevator2 = TestBuilders.CreateElevator(id: 2, currentFloor: 0, elevatorMovementTicks: 1, doorOpenTicks: 3);
            elevator2.AddDestination(8);
            elevator2.ProcessTick(); // IDLE → MOVING
            elevator2.ProcessTick(); // Move to floor 1
            
            var elevators = new List<Elevator> { elevator1, elevator2 };
            
            // Act
            var selected = strategy.SelectBestElevator(hallCall, elevators);
            
            // Assert
            // Elevator 1: distance 4 floors * 1 tick + 1 intermediate stop * 3 ticks = 4 + 3 = 7 ticks + load penalty (2 stops * 2 = 4) = 11 ticks
            // Elevator 2: distance 4 floors * 1 tick + 0 intermediate stops = 4 ticks + load penalty (1 stop * 2 = 2) = 6 ticks
            // Elevator 2 should win (6 < 11)
            Assert.Equal(2, selected!.Id);
        }

        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P1")]
        public void SelectBestElevator_SameDirectionRouteExtension_IncludesCompleteRouteTime()
        {
            // Test: Same direction elevator needs route extension (hall call beyond furthest destination)
            
            // Arrange
            var strategy = new DirectionAwareStrategy();
            var hallCall = new HallCall(8, Direction.UP);
            
            // MOVING elevator at floor 1, going to floor 5 (hall call at 8 requires route extension)
            var elevator1 = TestBuilders.CreateElevator(id: 1, currentFloor: 0, elevatorMovementTicks: 1, doorOpenTicks: 3);
            elevator1.AddDestination(5);
            elevator1.ProcessTick(); // IDLE → MOVING
            elevator1.ProcessTick(); // Move to floor 1
            
            // IDLE elevator at floor 0
            var elevator2 = TestBuilders.CreateElevator(id: 2, currentFloor: 0, elevatorMovementTicks: 1, doorOpenTicks: 3);
            
            var elevators = new List<Elevator> { elevator1, elevator2 };
            
            // Act
            var selected = strategy.SelectBestElevator(hallCall, elevators);
            
            // Assert
            // Elevator 1: time to complete route (4 floors * 1 tick) + time to reach hall call (3 floors * 1 tick) + load penalty (1 stop * 2 = 2) = 4 + 3 + 2 = 9 ticks
            // Elevator 2: distance 8 floors * 1 tick = 8 ticks
            // Elevator 2 should win (8 < 9)
            Assert.Equal(2, selected!.Id);
        }

        // ========== Load Consideration Tests ==========

        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P1")]
        public void SelectBestElevator_LoadConsideration_PenalizesManyStops()
        {
            // Test: Elevator with more stops should have higher cost due to load penalty
            
            // Arrange
            var strategy = new DirectionAwareStrategy();
            var hallCall = new HallCall(5, Direction.UP);
            
            // MOVING elevator at floor 1, going to floor 8, with 1 stop
            var elevator1 = TestBuilders.CreateElevator(id: 1, currentFloor: 0, elevatorMovementTicks: 1, doorOpenTicks: 3);
            elevator1.AddDestination(8);
            elevator1.ProcessTick(); // IDLE → MOVING
            elevator1.ProcessTick(); // Move to floor 1
            
            // MOVING elevator at floor 1, going to floor 8, with 3 stops
            var elevator2 = TestBuilders.CreateElevator(id: 2, currentFloor: 0, elevatorMovementTicks: 1, doorOpenTicks: 3);
            elevator2.AddDestination(3);
            elevator2.AddDestination(4);
            elevator2.AddDestination(8);
            elevator2.ProcessTick(); // IDLE → MOVING
            elevator2.ProcessTick(); // Move to floor 1
            
            var elevators = new List<Elevator> { elevator1, elevator2 };
            
            // Act
            var selected = strategy.SelectBestElevator(hallCall, elevators);
            
            // Assert
            // Elevator 1: distance 4 floors * 1 tick = 4 ticks + load penalty (1 stop * 2 = 2) = 6 ticks
            // Elevator 2: distance 4 floors * 1 tick + 2 intermediate stops * 3 ticks = 4 + 6 = 10 ticks + load penalty (3 stops * 2 = 6) = 16 ticks
            // Elevator 1 should win (6 < 16)
            Assert.Equal(1, selected!.Id);
        }

        // ========== Timeout-Based Opposite Direction Fallback Tests ==========

        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P1")]
        public void SelectBestElevator_TimeoutFallback_DoesNotUseOppositeDirectionWhenTooRecent()
        {
            // Test: Should NOT use opposite direction elevators if hall call is less than 10 seconds old
            
            // Arrange
            var strategy = new DirectionAwareStrategy();
            var hallCall = CreateHallCallWithAge(5, Direction.UP, TimeSpan.FromSeconds(5)); // 5 seconds old (not timed out)
            
            // Only opposite direction elevator available
            var elevator1 = TestBuilders.CreateElevator(id: 1, currentFloor: 0);
            elevator1.AddDestination(8);
            for (int i = 0; i < 10; i++) elevator1.ProcessTick(); // Move to floor 8
            elevator1.AddDestination(2); // Now going DOWN
            elevator1.ProcessTick(); // Start moving DOWN
            
            var elevators = new List<Elevator> { elevator1 };
            
            // Act
            var selected = strategy.SelectBestElevator(hallCall, elevators);
            
            // Assert
            Assert.Null(selected); // Should not use opposite direction (not timed out)
        }

        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P1")]
        public void SelectBestElevator_TimeoutFallback_UsesOppositeDirectionWhenTimedOut()
        {
            // Test: Should use opposite direction elevators if hall call is >= 10 seconds old
            
            // Arrange
            var strategy = new DirectionAwareStrategy();
            var hallCall = CreateHallCallWithAge(5, Direction.UP, TimeSpan.FromSeconds(15)); // 15 seconds old (timed out)
            
            // Verify that the age was set correctly
            var actualAge = hallCall.GetAge();
            Assert.True(actualAge.TotalSeconds >= 10, "Hall call should be at least 10 seconds old for timeout test");
            
            // Only opposite direction elevator available
            var elevator1 = TestBuilders.CreateElevator(id: 1, currentFloor: 0, elevatorMovementTicks: 1, doorOpenTicks: 3);
            elevator1.AddDestination(8);
            // Move to floor 8: IDLE -> MOVING (1 tick), then move 0->1->2->...->8 (8 floors = 8 ticks), arrive at 8 (1 tick) = 10 ticks total
            for (int i = 0; i < 10; i++) elevator1.ProcessTick(); // Now at floor 8, LOADING
            // Wait for door to close (3 ticks for doorOpenTicks)
            for (int i = 0; i < 3; i++) elevator1.ProcessTick(); // Door closes, transitions to MOVING DOWN
            elevator1.AddDestination(2); // Add destination while moving DOWN
            // Process one more tick to ensure it's in MOVING state
            elevator1.ProcessTick();
            
            // Verify elevator is in correct state
            Assert.Equal(ElevatorState.MOVING, elevator1.State);
            Assert.Equal(Direction.DOWN, elevator1.Direction);
            
            var elevators = new List<Elevator> { elevator1 };
            
            // Act
            var selected = strategy.SelectBestElevator(hallCall, elevators);
            
            // Assert
            Assert.NotNull(selected); // Should use opposite direction (timed out)
            Assert.Equal(1, selected!.Id);
        }

        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P1")]
        public void SelectBestElevator_TimeoutFallback_PrefersNormalCandidatesOverOppositeDirection()
        {
            // Test: Even if timed out, should prefer normal candidates (same direction or IDLE) over opposite direction
            
            // Arrange
            var strategy = new DirectionAwareStrategy();
            var hallCall = CreateHallCallWithAge(5, Direction.UP, TimeSpan.FromSeconds(15)); // Timed out
            
            // IDLE elevator (normal candidate)
            var elevator1 = TestBuilders.CreateElevator(id: 1, currentFloor: 0);
            
            // Opposite direction elevator
            var elevator2 = TestBuilders.CreateElevator(id: 2, currentFloor: 0);
            elevator2.AddDestination(8);
            for (int i = 0; i < 10; i++) elevator2.ProcessTick(); // Move to floor 8
            elevator2.AddDestination(2); // Now going DOWN
            elevator2.ProcessTick(); // Start moving DOWN
            
            var elevators = new List<Elevator> { elevator1, elevator2 };
            
            // Act
            var selected = strategy.SelectBestElevator(hallCall, elevators);
            
            // Assert
            Assert.NotNull(selected);
            Assert.Equal(1, selected!.Id); // IDLE elevator should be preferred
        }

        // ========== Edge Cases ==========

        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void SelectBestElevator_TieBreaker_UsesLowerId()
        {
            // Test: When two elevators have the same cost, prefer lower ID
            
            // Arrange
            var strategy = new DirectionAwareStrategy();
            var hallCall = new HallCall(5, Direction.UP);
            
            // Two IDLE elevators at the same floor (same cost)
            var elevator1 = TestBuilders.CreateElevator(id: 1, currentFloor: 0);
            var elevator2 = TestBuilders.CreateElevator(id: 2, currentFloor: 0);
            
            var elevators = new List<Elevator> { elevator1, elevator2 };
            
            // Act
            var selected = strategy.SelectBestElevator(hallCall, elevators);
            
            // Assert
            Assert.Equal(1, selected!.Id); // Lower ID wins
        }

        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void SelectBestElevator_RealTimeConfiguration_UsesElevatorSpecificTicks()
        {
            // Test: Uses real-time configuration ticks from each elevator (movementTicks, doorOpenDuration)
            
            // Arrange
            var strategy = new DirectionAwareStrategy();
            var hallCall = new HallCall(5, Direction.UP);
            
            // IDLE elevator with slow movement (movementTicks = 3)
            var slowElevator = TestBuilders.CreateElevator(id: 1, currentFloor: 0, elevatorMovementTicks: 3, doorOpenTicks: 3);
            
            // IDLE elevator with fast movement (movementTicks = 1)
            var fastElevator = TestBuilders.CreateElevator(id: 2, currentFloor: 0, elevatorMovementTicks: 1, doorOpenTicks: 3);
            
            var elevators = new List<Elevator> { slowElevator, fastElevator };
            
            // Act
            var selected = strategy.SelectBestElevator(hallCall, elevators);
            
            // Assert
            // Slow: 5 floors * 3 ticks = 15 ticks
            // Fast: 5 floors * 1 tick = 5 ticks
            // Fast should win
            Assert.Equal(2, selected!.Id);
        }
    }
}
