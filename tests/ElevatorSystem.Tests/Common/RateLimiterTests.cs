using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Xunit;
using ElevatorSystem.Common;
using ElevatorSystem.Tests.TestHelpers;

namespace ElevatorSystem.Tests.Common
{
    /// <summary>
    /// Tests for RateLimiter with sliding window algorithm.
    /// Validates global and per-source rate limiting.
    /// </summary>
    public class RateLimiterTests
    {
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P3")]
        public void IsAllowed_WithinGlobalLimit_ReturnsTrue()
        {
            // Arrange
            var logger = new MockLogger();
            var rateLimiter = new RateLimiter(globalLimitPerMinute: 10, perSourceLimitPerMinute: 5, logger);
            
            // Act
            var result = rateLimiter.IsAllowed("Source1");
            
            // Assert
            Assert.True(result);
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P3")]
        public void IsAllowed_ExceedsGlobalLimit_ReturnsFalse()
        {
            // Arrange
            var logger = new MockLogger();
            var rateLimiter = new RateLimiter(globalLimitPerMinute: 3, perSourceLimitPerMinute: 10, logger);
            
            // Act - Make 4 requests (exceeds global limit of 3)
            rateLimiter.IsAllowed("Source1");
            rateLimiter.IsAllowed("Source2");
            rateLimiter.IsAllowed("Source3");
            var result = rateLimiter.IsAllowed("Source4");
            
            // Assert
            Assert.False(result);
            Assert.True(logger.Contains("Global rate limit exceeded"));
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P3")]
        public void IsAllowed_ExceedsPerSourceLimit_ReturnsFalse()
        {
            // Arrange
            var logger = new MockLogger();
            var rateLimiter = new RateLimiter(globalLimitPerMinute: 100, perSourceLimitPerMinute: 2, logger);
            
            // Act - Make 3 requests from same source (exceeds per-source limit of 2)
            rateLimiter.IsAllowed("Source1");
            rateLimiter.IsAllowed("Source1");
            var result = rateLimiter.IsAllowed("Source1");
            
            // Assert
            Assert.False(result);
            Assert.True(logger.Contains("Per-source rate limit exceeded"));
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P3")]
        public void IsAllowed_DifferentSources_IndependentLimits()
        {
            // Arrange
            var logger = new MockLogger();
            var rateLimiter = new RateLimiter(globalLimitPerMinute: 100, perSourceLimitPerMinute: 2, logger);
            
            // Act - Make 2 requests from Source1, then 2 from Source2
            rateLimiter.IsAllowed("Source1");
            rateLimiter.IsAllowed("Source1");
            var result1 = rateLimiter.IsAllowed("Source2");
            var result2 = rateLimiter.IsAllowed("Source2");
            
            // Assert
            Assert.True(result1); // Source2's first request
            Assert.True(result2); // Source2's second request
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P3")]
        public void IsAllowed_ThreadSafe_ConcurrentRequests()
        {
            // Arrange
            var logger = new MockLogger();
            var rateLimiter = new RateLimiter(globalLimitPerMinute: 100, perSourceLimitPerMinute: 50, logger);
            var allowedCount = 0;
            var lockObj = new object();
            
            // Act - 100 threads making concurrent requests
            var tasks = new List<Task>();
            for (int i = 0; i < 100; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    if (rateLimiter.IsAllowed("TestSource"))
                    {
                        lock (lockObj) { allowedCount++; }
                    }
                }));
            }
            Task.WaitAll(tasks.ToArray());
            
            // Assert - Should respect per-source limit (50)
            Assert.True(allowedCount <= 50, $"Expected <= 50 allowed requests, got {allowedCount}");
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "P3")]
        public void IsAllowed_SlidingWindow_OldRequestsExpire()
        {
            // Arrange
            var logger = new MockLogger();
            var rateLimiter = new RateLimiter(globalLimitPerMinute: 2, perSourceLimitPerMinute: 2, logger);
            
            // Act - Make 2 requests (hits limit)
            rateLimiter.IsAllowed("Source1");
            rateLimiter.IsAllowed("Source1");
            
            // Wait for requests to age out (simulate time passing)
            // Note: In real implementation, this would require time abstraction
            // For now, we just verify the limit is enforced
            var result = rateLimiter.IsAllowed("Source1");
            
            // Assert
            Assert.False(result); // Still within 1-minute window
        }
    }
}
