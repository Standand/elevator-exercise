using System;
using Xunit;
using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.ValueObjects;

namespace ElevatorSystem.Tests.Domain.Entities
{
    /// <summary>
    /// Tests for HallCall entity state transitions.
    /// Validates state machine: PENDING → ASSIGNED → COMPLETED
    /// </summary>
    public class HallCallTests
    {
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void Constructor_CreatesHallCallWithPendingStatus()
        {
            // Act
            var hallCall = new HallCall(5, Direction.UP);
            
            // Assert
            Assert.Equal(5, hallCall.Floor);
            Assert.Equal(Direction.UP, hallCall.Direction);
            Assert.Equal(HallCallStatus.PENDING, hallCall.Status);
            Assert.Null(hallCall.AssignedElevatorId);
            Assert.NotEqual(Guid.Empty, hallCall.Id);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void MarkAsAssigned_FromPending_UpdatesStatus()
        {
            // Arrange
            var hallCall = new HallCall(5, Direction.UP);
            
            // Act
            hallCall.MarkAsAssigned(elevatorId: 2);
            
            // Assert
            Assert.Equal(HallCallStatus.ASSIGNED, hallCall.Status);
            Assert.Equal(2, hallCall.AssignedElevatorId);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void MarkAsAssigned_FromNonPending_ThrowsException()
        {
            // Arrange
            var hallCall = new HallCall(5, Direction.UP);
            hallCall.MarkAsAssigned(1);
            
            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => hallCall.MarkAsAssigned(2));
            Assert.Contains("Cannot assign", ex.Message);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void MarkAsCompleted_FromAssigned_UpdatesStatus()
        {
            // Arrange
            var hallCall = new HallCall(5, Direction.UP);
            hallCall.MarkAsAssigned(1);
            
            // Act
            hallCall.MarkAsCompleted();
            
            // Assert
            Assert.Equal(HallCallStatus.COMPLETED, hallCall.Status);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void MarkAsCompleted_FromPending_ThrowsException()
        {
            // Arrange
            var hallCall = new HallCall(5, Direction.UP);
            
            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => hallCall.MarkAsCompleted());
            Assert.Contains("Cannot complete", ex.Message);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void ToString_ReturnsFormattedString()
        {
            // Arrange
            var hallCall = new HallCall(5, Direction.UP);
            
            // Act
            var result = hallCall.ToString();
            
            // Assert
            Assert.Contains("HallCall", result);
            Assert.Contains("5", result);
            Assert.Contains("UP", result);
            Assert.Contains("PENDING", result);
        }
    }
}
