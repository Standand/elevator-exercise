using System;
using Xunit;
using ElevatorSystem.Common;

namespace ElevatorSystem.Tests.Common
{
    /// <summary>
    /// Tests for Result<T> pattern.
    /// Validates success/failure creation and pattern matching.
    /// </summary>
    public class ResultTests
    {
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P3")]
        public void Success_CreatesSuccessResult()
        {
            // Act
            var result = Result<int>.Success(42);
            
            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(42, result.Value);
            Assert.Null(result.Error);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P3")]
        public void Success_NullValue_ThrowsException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => Result<string>.Success(null!));
            Assert.Contains("Success value cannot be null", ex.Message);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P3")]
        public void Failure_CreatesFailureResult()
        {
            // Act
            var result = Result<int>.Failure("Something went wrong");
            
            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(0, result.Value); // Default value for int
            Assert.Equal("Something went wrong", result.Error);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P3")]
        public void Failure_EmptyError_ThrowsException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => Result<int>.Failure(""));
            Assert.Contains("Error message cannot be empty", ex.Message);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P3")]
        public void Match_Success_ExecutesSuccessBranch()
        {
            // Arrange
            var result = Result<int>.Success(42);
            
            // Act
            var output = result.Match(
                onSuccess: v => $"Value: {v}",
                onFailure: e => $"Error: {e}");
            
            // Assert
            Assert.Equal("Value: 42", output);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P3")]
        public void Match_Failure_ExecutesFailureBranch()
        {
            // Arrange
            var result = Result<int>.Failure("Error occurred");
            
            // Act
            var output = result.Match(
                onSuccess: v => $"Value: {v}",
                onFailure: e => $"Error: {e}");
            
            // Assert
            Assert.Equal("Error: Error occurred", output);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P3")]
        public void ToString_Success_ReturnsFormattedString()
        {
            // Arrange
            var result = Result<int>.Success(42);
            
            // Act
            var str = result.ToString();
            
            // Assert
            Assert.Contains("Success", str);
            Assert.Contains("42", str);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P3")]
        public void ToString_Failure_ReturnsFormattedString()
        {
            // Arrange
            var result = Result<int>.Failure("Error occurred");
            
            // Act
            var str = result.ToString();
            
            // Assert
            Assert.Contains("Failure", str);
            Assert.Contains("Error occurred", str);
        }
    }
}
