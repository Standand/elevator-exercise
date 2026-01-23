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
    /// Validates elevator selection logic based on direction and distance.
    /// </summary>
    public class DirectionAwareStrategyTests
    {
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void SelectBestElevator_PrioritizesSameDirection()
        {
            // Arrange
            var strategy = new DirectionAwareStrategy();
            var hallCall = new HallCall(5, Direction.UP);
            
            var elevator1 = TestBuilders.CreateElevator(id: 1, currentFloor: 0); // Idle at 0
            
            var elevator2 = TestBuilders.CreateElevator(id: 2, currentFloor: 0); // Moving UP
            elevator2.AddDestination(8);
            elevator2.ProcessTick(); // IDLE → MOVING UP
            
            var elevators = new List<Elevator> { elevator1, elevator2 };
            
            // Act
            var selected = strategy.SelectBestElevator(hallCall, elevators);
            
            // Assert
            Assert.Equal(2, selected!.Id); // Elevator 2 (same direction) preferred
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
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
        [Trait("Priority", "P2")]
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
        [Trait("Priority", "P2")]
        public void SelectBestElevator_NoElevatorsAvailable_ReturnsNull()
        {
            // Arrange
            var strategy = new DirectionAwareStrategy();
            var hallCall = new HallCall(5, Direction.UP);
            
            var elevator1 = TestBuilders.CreateElevator(id: 1, currentFloor: 0);
            elevator1.AddDestination(5);
            elevator1.ProcessTick(); // IDLE → MOVING
            for (int i = 0; i < 6; i++) elevator1.ProcessTick(); // Move to floor 5 and LOADING
            
            var elevators = new List<Elevator> { elevator1 };
            
            // Act
            var selected = strategy.SelectBestElevator(hallCall, elevators);
            
            // Assert
            Assert.Null(selected); // Cannot accept (already at floor 5 in LOADING)
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
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
    }
}
