using System;
using Xunit;
using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.ValueObjects;

namespace ElevatorSystem.Tests.Domain.Entities
{
    /// <summary>
    /// Tests for Request entity state transitions.
    /// Validates state machine: WAITING → IN_TRANSIT → COMPLETED
    /// </summary>
    public class RequestTests
    {
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void Constructor_CreatesRequestWithWaitingStatus()
        {
            // Arrange
            var journey = Journey.Of(3, 7);
            var hallCallId = Guid.NewGuid();
            
            // Act
            var request = new Request(hallCallId, journey);
            
            // Assert
            Assert.Equal(hallCallId, request.HallCallId);
            Assert.Equal(journey, request.Journey);
            Assert.Equal(RequestStatus.WAITING, request.Status);
            Assert.NotEqual(Guid.Empty, request.Id);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void MarkAsInTransit_FromWaiting_UpdatesStatus()
        {
            // Arrange
            var journey = Journey.Of(3, 7);
            var hallCallId = Guid.NewGuid();
            var request = new Request(hallCallId, journey);
            
            // Act
            request.MarkAsInTransit();
            
            // Assert
            Assert.Equal(RequestStatus.IN_TRANSIT, request.Status);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void MarkAsInTransit_FromNonWaiting_ThrowsException()
        {
            // Arrange
            var journey = Journey.Of(3, 7);
            var request = new Request(Guid.NewGuid(), journey);
            request.MarkAsInTransit();
            
            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => request.MarkAsInTransit());
            Assert.Contains("Cannot mark request as in-transit", ex.Message);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void MarkAsCompleted_FromInTransit_UpdatesStatus()
        {
            // Arrange
            var journey = Journey.Of(3, 7);
            var request = new Request(Guid.NewGuid(), journey);
            request.MarkAsInTransit();
            
            // Act
            request.MarkAsCompleted();
            
            // Assert
            Assert.Equal(RequestStatus.COMPLETED, request.Status);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void MarkAsCompleted_FromWaiting_ThrowsException()
        {
            // Arrange
            var journey = Journey.Of(3, 7);
            var request = new Request(Guid.NewGuid(), journey);
            
            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => request.MarkAsCompleted());
            Assert.Contains("Cannot complete request", ex.Message);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void ToString_ReturnsFormattedString()
        {
            // Arrange
            var journey = Journey.Of(3, 7);
            var request = new Request(Guid.NewGuid(), journey);
            
            // Act
            var result = request.ToString();
            
            // Assert
            Assert.Contains("Request", result);
            Assert.Contains("WAITING", result);
        }
    }
}
