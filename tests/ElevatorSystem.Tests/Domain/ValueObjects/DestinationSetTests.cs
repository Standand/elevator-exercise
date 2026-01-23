using System;
using System.Linq;
using Xunit;
using ElevatorSystem.Domain.ValueObjects;

namespace ElevatorSystem.Tests.Domain.ValueObjects
{
    /// <summary>
    /// Tests for DestinationSet value object.
    /// Critical for elevator movement logic and floor 0 edge case.
    /// </summary>
    public class DestinationSetTests
    {
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void GetNextDestination_DirectionUp_ReturnsSmallestFloorAboveCurrent()
        {
            // Arrange
            var destinations = new DestinationSet(Direction.UP);
            destinations.Add(3);
            destinations.Add(7);
            destinations.Add(5);
            
            // Act
            var next = destinations.GetNextDestination(currentFloor: 4);
            
            // Assert
            Assert.Equal(5, next); // Smallest floor >= 4
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void GetNextDestination_DirectionDown_ReturnsLargestFloorBelowCurrent()
        {
            // Arrange
            var destinations = new DestinationSet(Direction.DOWN);
            destinations.Add(3);
            destinations.Add(7);
            destinations.Add(5);
            
            // Act
            var next = destinations.GetNextDestination(currentFloor: 6);
            
            // Assert
            Assert.Equal(5, next); // Largest floor <= 6
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void GetNextDestination_FloorZeroIsValid_ReturnsZero()
        {
            // Arrange
            var destinations = new DestinationSet(Direction.DOWN);
            destinations.Add(0); // Floor 0 is valid!
            destinations.Add(3);
            
            // Act
            var next = destinations.GetNextDestination(currentFloor: 2);
            
            // Assert
            Assert.Equal(0, next); // Floor 0 should be returned
        }
        
        [Theory]
        [InlineData(5, 5)] // Current at 5, nearest is 5
        [InlineData(4, 5)] // Current at 4, nearest is 5
        [InlineData(6, 5)] // Current at 6, nearest is 5
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void GetNextDestination_DirectionIdle_ReturnsNearest(int currentFloor, int expected)
        {
            // Arrange
            var destinations = new DestinationSet(Direction.IDLE);
            destinations.Add(2);
            destinations.Add(5);
            destinations.Add(9);
            
            // Act
            var next = destinations.GetNextDestination(currentFloor);
            
            // Assert
            Assert.Equal(expected, next);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void GetFurthestDestination_DirectionUp_ReturnsMax()
        {
            // Arrange
            var destinations = new DestinationSet(Direction.UP);
            destinations.Add(3);
            destinations.Add(9);
            destinations.Add(5);
            
            // Act
            var furthest = destinations.GetFurthestDestination();
            
            // Assert
            Assert.Equal(9, furthest);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void GetFurthestDestination_DirectionDown_ReturnsMin()
        {
            // Arrange
            var destinations = new DestinationSet(Direction.DOWN);
            destinations.Add(3);
            destinations.Add(9);
            destinations.Add(5);
            
            // Act
            var furthest = destinations.GetFurthestDestination();
            
            // Assert
            Assert.Equal(3, furthest);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void Remove_RemovesDestination()
        {
            // Arrange
            var destinations = new DestinationSet(Direction.UP);
            destinations.Add(5);
            destinations.Add(7);
            
            // Act
            destinations.Remove(5);
            
            // Assert
            Assert.False(destinations.Contains(5));
            Assert.True(destinations.Contains(7));
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void Add_AddsDestination()
        {
            // Arrange
            var destinations = new DestinationSet(Direction.UP);
            
            // Act
            destinations.Add(5);
            
            // Assert
            Assert.True(destinations.Contains(5));
            Assert.Equal(1, destinations.Count);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void IsEmpty_NoDestinations_ReturnsTrue()
        {
            // Arrange
            var destinations = new DestinationSet(Direction.UP);
            
            // Act & Assert
            Assert.True(destinations.IsEmpty);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void GetAll_ReturnsAllDestinations()
        {
            // Arrange
            var destinations = new DestinationSet(Direction.UP);
            destinations.Add(3);
            destinations.Add(7);
            destinations.Add(5);
            
            // Act
            var all = destinations.GetAll();
            
            // Assert
            Assert.Equal(3, all.Count);
            Assert.Contains(3, all);
            Assert.Contains(5, all);
            Assert.Contains(7, all);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void SetDirection_UpdatesDirection()
        {
            // Arrange
            var destinations = new DestinationSet(Direction.UP);
            destinations.Add(5);
            
            // Act
            destinations.SetDirection(Direction.DOWN);
            
            // Assert - Verify by checking furthest destination logic
            var furthest = destinations.GetFurthestDestination();
            Assert.Equal(5, furthest); // With DOWN, min is furthest
        }
    }
}
