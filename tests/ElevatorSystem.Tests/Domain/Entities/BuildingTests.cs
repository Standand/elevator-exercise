using System;
using System.Linq;
using Xunit;
using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.ValueObjects;
using ElevatorSystem.Tests.TestHelpers;

namespace ElevatorSystem.Tests.Domain.Entities
{
    /// <summary>
    /// Tests for the Building entity (aggregate root).
    /// Covers request validation, rate limiting, queue capacity, and tick processing.
    /// </summary>
    public class BuildingTests
    {
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P1")]
        public void RequestHallCall_ValidRequest_ReturnsSuccess()
        {
            // Arrange
            var building = TestBuilders.CreateBuilding();
            
            // Act
            var result = building.RequestHallCall(5, Direction.UP, "TestSource");
            
            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Value);
            Assert.Equal(5, result.Value.Floor);
            Assert.Equal(Direction.UP, result.Value.Direction);
            Assert.Equal(HallCallStatus.PENDING, result.Value.Status);
        }
        
        [Theory]
        [InlineData(-1)]
        [InlineData(11)]
        [InlineData(100)]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P1")]
        public void RequestHallCall_FloorOutOfRange_ReturnsFailure(int invalidFloor)
        {
            // Arrange
            var building = TestBuilders.CreateBuilding(maxFloors: 10);
            
            // Act
            var result = building.RequestHallCall(invalidFloor, Direction.UP);
            
            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("out of range", result.Error, StringComparison.OrdinalIgnoreCase);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P1")]
        public void RequestHallCall_InvalidDirection_ReturnsFailure()
        {
            // Arrange
            var building = TestBuilders.CreateBuilding();
            
            // Act
            var result = building.RequestHallCall(5, Direction.IDLE);
            
            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("Invalid direction", result.Error);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P1")]
        public void RequestHallCall_DuplicateRequest_ReturnsExistingHallCall()
        {
            // Arrange
            var building = TestBuilders.CreateBuilding();
            var first = building.RequestHallCall(5, Direction.UP);
            
            // Act
            var second = building.RequestHallCall(5, Direction.UP);
            
            // Assert
            Assert.True(second.IsSuccess);
            Assert.Equal(first.Value.Id, second.Value.Id); // Same hall call
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P1")]
        public void RequestHallCall_RateLimitExceeded_ReturnsFailure()
        {
            // Arrange
            var building = TestBuilders.CreateBuilding();
            
            // Act - Make 11 requests (limit is 10 per source per minute)
            for (int i = 0; i < 10; i++)
            {
                building.RequestHallCall(i, Direction.UP, "TestSource");
            }
            var result = building.RequestHallCall(5, Direction.DOWN, "TestSource");
            
            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("Rate limit exceeded", result.Error);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P1")]
        public void RequestHallCall_QueueAtCapacity_ReturnsFailure()
        {
            // Arrange
            var building = TestBuilders.CreateBuilding(maxFloors: 10); // Max 18 hall calls (maxFloors * 2 - 2)
            
            // Act - Fill queue to capacity (18 hall calls)
            // Floors 1-9 can have UP and DOWN (9 * 2 = 18)
            for (int floor = 1; floor < 10; floor++)
            {
                building.RequestHallCall(floor, Direction.UP, $"SourceA{floor}");
                building.RequestHallCall(floor, Direction.DOWN, $"SourceB{floor}");
            }
            
            // Now try to add one more - should fail
            var result = building.RequestHallCall(0, Direction.UP, "OverflowSource");
            
            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("capacity", result.Error, StringComparison.OrdinalIgnoreCase);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P1")]
        public void ProcessTick_PendingHallCall_GetsAssigned()
        {
            // Arrange
            var building = TestBuilders.CreateBuilding();
            var hallCall = building.RequestHallCall(5, Direction.UP).Value;
            
            // Act
            building.ProcessTick(); // Should assign to an elevator
            
            // Assert
            var status = building.GetStatus();
            Assert.Equal(HallCallStatus.ASSIGNED, hallCall.Status);
            Assert.True(status.Elevators.Any(e => e.Destinations.Contains(5)));
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P1")]
        public void GetStatus_ReturnsCurrentState()
        {
            // Arrange
            var building = TestBuilders.CreateBuilding(elevatorCount: 4, maxFloors: 10);
            building.RequestHallCall(5, Direction.UP);
            
            // Act
            var status = building.GetStatus();
            
            // Assert
            Assert.Equal(4, status.Elevators.Count);
            Assert.Equal(1, status.PendingHallCallsCount);
            Assert.NotNull(status.Timestamp);
        }
    }
}
