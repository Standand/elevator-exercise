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
    /// Tests for NearestFirstStrategy scheduling algorithm.
    /// Validates simple nearest-elevator selection logic.
    /// </summary>
    public class NearestFirstStrategyTests
    {
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void SelectBestElevator_PicksNearestElevator()
        {
            // Arrange
            var strategy = new NearestFirstStrategy();
            var hallCall = new HallCall(5, Direction.UP);
            
            var elevator1 = TestBuilders.CreateElevator(id: 1, currentFloor: 0); // At floor 0, IDLE
            var elevator2 = TestBuilders.CreateElevator(id: 2, currentFloor: 0); // At floor 0
            
            // Move elevator2 to floor 4 and make it IDLE
            elevator2.AddDestination(4);
            // IDLE -> MOVING (tick 1), then move 1->2->3->4 (ticks 2-5), arrive at 4 (tick 6)
            for (int i = 0; i < 6; i++) elevator2.ProcessTick(); // Now at floor 4, LOADING
            // Wait for door timer (default 3 ticks) to complete LOADING -> IDLE
            for (int i = 0; i < 3; i++) elevator2.ProcessTick();
            // Now elevator2 is IDLE at floor 4
            
            var elevators = new List<Elevator> { elevator1, elevator2 };
            
            // Act
            var selected = strategy.SelectBestElevator(hallCall, elevators);
            
            // Assert
            Assert.Equal(2, selected!.Id); // Elevator 2 is nearer (distance 1 vs 5)
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void SelectBestElevator_NoElevatorsAvailable_ReturnsNull()
        {
            // Arrange
            var strategy = new NearestFirstStrategy();
            var hallCall = new HallCall(5, Direction.UP);
            
            var elevator1 = TestBuilders.CreateElevator(id: 1, currentFloor: 0);
            elevator1.AddDestination(5);
            for (int i = 0; i < 7; i++) elevator1.ProcessTick(); // At floor 5 in LOADING
            
            var elevators = new List<Elevator> { elevator1 };
            
            // Act
            var selected = strategy.SelectBestElevator(hallCall, elevators);
            
            // Assert
            Assert.Null(selected);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void SelectBestElevator_EmptyList_ReturnsNull()
        {
            // Arrange
            var strategy = new NearestFirstStrategy();
            var hallCall = new HallCall(5, Direction.UP);
            var elevators = new List<Elevator>();
            
            // Act
            var selected = strategy.SelectBestElevator(hallCall, elevators);
            
            // Assert
            Assert.Null(selected);
        }
    }
}
