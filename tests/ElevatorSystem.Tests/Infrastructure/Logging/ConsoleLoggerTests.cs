using System;
using System.IO;
using Xunit;
using ElevatorSystem.Infrastructure.Logging;

namespace ElevatorSystem.Tests.Infrastructure.Logging
{
    /// <summary>
    /// Tests for ConsoleLogger output formatting.
    /// Validates debug flag and message formatting.
    /// </summary>
    public class ConsoleLoggerTests
    {
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P3")]
        public void LogDebug_DebugEnabled_WritesToOutput()
        {
            // Arrange
            var logger = new ConsoleLogger(enableDebug: true);
            var output = new StringWriter();
            Console.SetOut(output);
            
            try
            {
                // Act
                logger.LogDebug("Debug message");
                var result = output.ToString();
                
                // Assert
                Assert.Contains("DEBUG", result);
                Assert.Contains("Debug message", result);
            }
            finally
            {
                // Restore console
                var standardOutput = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
                Console.SetOut(standardOutput);
            }
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P3")]
        public void LogDebug_DebugDisabled_DoesNotWriteToOutput()
        {
            // Arrange
            var logger = new ConsoleLogger(enableDebug: false);
            var output = new StringWriter();
            Console.SetOut(output);
            
            try
            {
                // Act
                logger.LogDebug("Debug message");
                var result = output.ToString();
                
                // Assert
                Assert.DoesNotContain("DEBUG", result);
            }
            finally
            {
                // Restore console
                var standardOutput = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
                Console.SetOut(standardOutput);
            }
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P3")]
        public void LogInfo_AlwaysWritesToOutput()
        {
            // Arrange
            var logger = new ConsoleLogger(enableDebug: false);
            var output = new StringWriter();
            Console.SetOut(output);
            
            try
            {
                // Act
                logger.LogInfo("Info message");
                var result = output.ToString();
                
                // Assert
                Assert.Contains("INFO", result);
                Assert.Contains("Info message", result);
            }
            finally
            {
                // Restore console
                var standardOutput = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
                Console.SetOut(standardOutput);
            }
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P3")]
        public void LogWarning_AlwaysWritesToOutput()
        {
            // Arrange
            var logger = new ConsoleLogger(enableDebug: false);
            var output = new StringWriter();
            Console.SetOut(output);
            
            try
            {
                // Act
                logger.LogWarning("Warning message");
                var result = output.ToString();
                
                // Assert
                Assert.Contains("WARN", result);
                Assert.Contains("Warning message", result);
            }
            finally
            {
                // Restore console
                var standardOutput = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
                Console.SetOut(standardOutput);
            }
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P3")]
        public void LogError_AlwaysWritesToOutput()
        {
            // Arrange
            var logger = new ConsoleLogger(enableDebug: false);
            var output = new StringWriter();
            Console.SetOut(output);
            
            try
            {
                // Act
                logger.LogError("Error message");
                var result = output.ToString();
                
                // Assert
                Assert.Contains("ERROR", result);
                Assert.Contains("Error message", result);
            }
            finally
            {
                // Restore console
                var standardOutput = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
                Console.SetOut(standardOutput);
            }
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P3")]
        public void DebugFiltering_OnlyFiltersDebugMessages()
        {
            // Arrange
            var logger = new ConsoleLogger(enableDebug: false);
            var output = new StringWriter();
            Console.SetOut(output);
            
            try
            {
                // Act
                logger.LogDebug("Debug");
                logger.LogInfo("Info");
                logger.LogWarning("Warning");
                logger.LogError("Error");
                var result = output.ToString();
                
                // Assert
                Assert.DoesNotContain("DEBUG", result);
                Assert.Contains("INFO", result);
                Assert.Contains("WARN", result);
                Assert.Contains("ERROR", result);
            }
            finally
            {
                // Restore console
                var standardOutput = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
                Console.SetOut(standardOutput);
            }
        }
    }
}
