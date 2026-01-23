using System;
using System.Linq;
using Xunit;
using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.ValueObjects;

namespace ElevatorSystem.Tests.Domain.ValueObjects
{
    /// <summary>
    /// Tests for HallCallQueue value object.
    /// Validates queue operations and hall call retrieval logic.
    /// </summary>
    public class HallCallQueueTests
    {
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void Add_AddsHallCallToQueue()
        {
            // Arrange
            var queue = new HallCallQueue();
            var hallCall = new HallCall(5, Direction.UP);
            
            // Act
            queue.Add(hallCall);
            
            // Assert
            var all = queue.GetAll();
            Assert.Single(all);
            Assert.Contains(hallCall, all);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void FindByFloorAndDirection_ExistingHallCall_ReturnsHallCall()
        {
            // Arrange
            var queue = new HallCallQueue();
            var hallCall = new HallCall(5, Direction.UP);
            queue.Add(hallCall);
            
            // Act
            var found = queue.FindByFloorAndDirection(5, Direction.UP);
            
            // Assert
            Assert.NotNull(found);
            Assert.Equal(hallCall.Id, found.Id);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void FindByFloorAndDirection_NonExistingHallCall_ReturnsNull()
        {
            // Arrange
            var queue = new HallCallQueue();
            var hallCall = new HallCall(5, Direction.UP);
            queue.Add(hallCall);
            
            // Act
            var found = queue.FindByFloorAndDirection(7, Direction.UP);
            
            // Assert
            Assert.Null(found);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void FindByFloorAndDirection_CompletedHallCall_ReturnsNull()
        {
            // Arrange
            var queue = new HallCallQueue();
            var hallCall = new HallCall(5, Direction.UP);
            queue.Add(hallCall);
            hallCall.MarkAsAssigned(1);
            hallCall.MarkAsCompleted();
            
            // Act
            var found = queue.FindByFloorAndDirection(5, Direction.UP);
            
            // Assert
            Assert.Null(found); // Completed hall calls are not returned
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void GetPending_ReturnsPendingHallCallsOnly()
        {
            // Arrange
            var queue = new HallCallQueue();
            var pending1 = new HallCall(3, Direction.UP);
            var pending2 = new HallCall(7, Direction.UP);
            var assigned = new HallCall(5, Direction.UP);
            
            queue.Add(pending1);
            queue.Add(pending2);
            queue.Add(assigned);
            assigned.MarkAsAssigned(1);
            
            // Act
            var pending = queue.GetPending();
            
            // Assert
            Assert.Equal(2, pending.Count);
            Assert.Contains(pending1, pending);
            Assert.Contains(pending2, pending);
            Assert.DoesNotContain(assigned, pending);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void GetPendingCount_ReturnsCorrectCount()
        {
            // Arrange
            var queue = new HallCallQueue();
            var pending1 = new HallCall(3, Direction.UP);
            var pending2 = new HallCall(7, Direction.UP);
            var assigned = new HallCall(5, Direction.UP);
            
            queue.Add(pending1);
            queue.Add(pending2);
            queue.Add(assigned);
            assigned.MarkAsAssigned(1);
            
            // Act
            var count = queue.GetPendingCount();
            
            // Assert
            Assert.Equal(2, count);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void FindById_ExistingId_ReturnsHallCall()
        {
            // Arrange
            var queue = new HallCallQueue();
            var hallCall = new HallCall(5, Direction.UP);
            queue.Add(hallCall);
            
            // Act
            var found = queue.FindById(hallCall.Id);
            
            // Assert
            Assert.NotNull(found);
            Assert.Equal(hallCall.Id, found.Id);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void FindById_NonExistingId_ReturnsNull()
        {
            // Arrange
            var queue = new HallCallQueue();
            
            // Act
            var found = queue.FindById(Guid.NewGuid());
            
            // Assert
            Assert.Null(found);
        }
    }
}
