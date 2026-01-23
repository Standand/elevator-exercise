using System;
using Xunit;
using ElevatorSystem.Domain.ValueObjects;

namespace ElevatorSystem.Tests.Domain.ValueObjects
{
    /// <summary>
    /// Tests for value object factory methods and validation.
    /// Covers Direction, Journey, and status value objects.
    /// </summary>
    public class ValueObjectTests
    {
        #region Direction Tests
        
        [Theory]
        [InlineData("UP")]
        [InlineData("up")]
        [InlineData("Up")]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void Direction_Of_ValidValue_ReturnsCorrectInstance(string input)
        {
            // Act
            var direction = Direction.Of(input);
            
            // Assert
            Assert.Equal(Direction.UP, direction);
            Assert.Equal("UP", direction.Value);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void Direction_Of_InvalidValue_ThrowsException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => Direction.Of("LEFT"));
            Assert.Contains("Invalid direction", ex.Message);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void Direction_Equality_WorksCorrectly()
        {
            // Arrange
            var up1 = Direction.UP;
            var up2 = Direction.Of("UP");
            var down = Direction.DOWN;
            
            // Act & Assert
            Assert.Equal(up1, up2);
            Assert.True(up1 == up2);
            Assert.NotEqual(up1, down);
            Assert.True(up1 != down);
        }
        
        #endregion
        
        #region Journey Tests
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void Journey_Of_ValidJourney_CreatesInstance()
        {
            // Act
            var journey = Journey.Of(sourceFloor: 3, destinationFloor: 7);
            
            // Assert
            Assert.Equal(3, journey.SourceFloor);
            Assert.Equal(7, journey.DestinationFloor);
            Assert.Equal(Direction.UP, journey.Direction);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void Journey_Of_DownwardJourney_HasDownDirection()
        {
            // Act
            var journey = Journey.Of(sourceFloor: 7, destinationFloor: 3);
            
            // Assert
            Assert.Equal(7, journey.SourceFloor);
            Assert.Equal(3, journey.DestinationFloor);
            Assert.Equal(Direction.DOWN, journey.Direction);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void Journey_Of_SameFloor_ThrowsException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => Journey.Of(5, 5));
            Assert.Contains("cannot be the same", ex.Message);
        }
        
        [Theory]
        [InlineData(-1, 5)]
        [InlineData(5, -1)]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void Journey_Of_NegativeFloor_ThrowsException(int source, int destination)
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => Journey.Of(source, destination));
            Assert.Contains("cannot be negative", ex.Message);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void Journey_Of_BothNegative_ThrowsException()
        {
            // Act & Assert - When both are -1, "same floor" check happens first
            var ex = Assert.Throws<ArgumentException>(() => Journey.Of(-1, -1));
            Assert.Contains("cannot be the same", ex.Message);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void Journey_Equality_WorksCorrectly()
        {
            // Arrange
            var journey1 = Journey.Of(3, 7);
            var journey2 = Journey.Of(3, 7);
            var journey3 = Journey.Of(3, 8);
            
            // Act & Assert
            Assert.Equal(journey1, journey2);
            Assert.NotEqual(journey1, journey3);
        }
        
        #endregion
        
        #region ElevatorState Tests
        
        [Theory]
        [InlineData("IDLE")]
        [InlineData("idle")]
        [InlineData("Idle")]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void ElevatorState_Of_ValidValue_ReturnsCorrectInstance(string input)
        {
            // Act
            var state = ElevatorState.Of(input);
            
            // Assert
            Assert.Equal(ElevatorState.IDLE, state);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void ElevatorState_Of_InvalidValue_ThrowsException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => ElevatorState.Of("BROKEN"));
            Assert.Contains("Invalid elevator state", ex.Message);
        }
        
        #endregion
        
        #region HallCallStatus Tests
        
        [Theory]
        [InlineData("PENDING")]
        [InlineData("pending")]
        [InlineData("Pending")]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void HallCallStatus_Of_ValidValue_ReturnsCorrectInstance(string input)
        {
            // Act
            var status = HallCallStatus.Of(input);
            
            // Assert
            Assert.Equal(HallCallStatus.PENDING, status);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void HallCallStatus_Of_InvalidValue_ThrowsException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => HallCallStatus.Of("INVALID"));
            Assert.Contains("Invalid hall call status", ex.Message);
        }
        
        #endregion
        
        #region RequestStatus Tests
        
        [Theory]
        [InlineData("WAITING")]
        [InlineData("waiting")]
        [InlineData("Waiting")]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void RequestStatus_Of_ValidValue_ReturnsCorrectInstance(string input)
        {
            // Act
            var status = RequestStatus.Of(input);
            
            // Assert
            Assert.Equal(RequestStatus.WAITING, status);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P2")]
        public void RequestStatus_Of_InvalidValue_ThrowsException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => RequestStatus.Of("INVALID"));
            Assert.Contains("Invalid request status", ex.Message);
        }
        
        #endregion
    }
}
