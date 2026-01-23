using ElevatorSystem.Common;
using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.Services;
using ElevatorSystem.Domain.ValueObjects;
using ElevatorSystem.Infrastructure.Metrics;
using ElevatorSystem.Tests.Integration.TestHelpers;
using ElevatorSystem.Tests.TestHelpers;

namespace ElevatorSystem.Tests.Integration
{
    /// <summary>
    /// Integration tests for rate limiting with Building.
    /// </summary>
    public class RateLimitingIntegrationTests
    {
        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Priority", "P2")]
        public void RequestHallCall_RateLimitExceeded_RejectsRequest()
        {
            var logger = new MockLogger();
            var rateLimiter = new RateLimiter(globalLimitPerMinute: 2, perSourceLimitPerMinute: 10, logger);
            var metrics = new SystemMetrics();
            
            var building = IntegrationTestBuilder.Create()
                .WithSchedulingStrategy(new DirectionAwareStrategy())
                .WithLogger(logger)
                .WithMetrics(metrics)
                .WithRateLimiter(rateLimiter)
                .Build();

            var firstResult = building.RequestHallCall(floor: 1, direction: Direction.UP, source: "TestSource");
            var secondResult = building.RequestHallCall(floor: 2, direction: Direction.UP, source: "TestSource");
            var thirdResult = building.RequestHallCall(floor: 3, direction: Direction.UP, source: "TestSource");
            
            Assert.True(firstResult.IsSuccess);
            Assert.True(secondResult.IsSuccess);
            Assert.False(thirdResult.IsSuccess);
            Assert.Contains("Rate limit exceeded", thirdResult.Error);
            
            var snapshot = metrics.GetSnapshot();
            Assert.True(snapshot.RateLimitHits >= 1);
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Priority", "P2")]
        public void RequestHallCall_PerSourceRateLimitExceeded_RejectsRequest()
        {
            var logger = new MockLogger();
            var rateLimiter = new RateLimiter(globalLimitPerMinute: 100, perSourceLimitPerMinute: 2, logger);
            var metrics = new SystemMetrics();
            
            var building = IntegrationTestBuilder.Create()
                .WithSchedulingStrategy(new DirectionAwareStrategy())
                .WithLogger(logger)
                .WithMetrics(metrics)
                .WithRateLimiter(rateLimiter)
                .Build();

            var firstResult = building.RequestHallCall(floor: 1, direction: Direction.UP, source: "Source1");
            var secondResult = building.RequestHallCall(floor: 2, direction: Direction.UP, source: "Source1");
            var thirdResult = building.RequestHallCall(floor: 3, direction: Direction.UP, source: "Source1");
            
            Assert.True(firstResult.IsSuccess);
            Assert.True(secondResult.IsSuccess);
            Assert.False(thirdResult.IsSuccess);
            
            var otherSourceResult = building.RequestHallCall(floor: 4, direction: Direction.UP, source: "Source2");
            Assert.True(otherSourceResult.IsSuccess);
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Priority", "P2")]
        public void RequestHallCall_DifferentSources_IndependentLimits()
        {
            var logger = new MockLogger();
            var rateLimiter = new RateLimiter(globalLimitPerMinute: 100, perSourceLimitPerMinute: 2, logger);
            var metrics = new SystemMetrics();
            
            var building = IntegrationTestBuilder.Create()
                .WithSchedulingStrategy(new DirectionAwareStrategy())
                .WithLogger(logger)
                .WithMetrics(metrics)
                .WithRateLimiter(rateLimiter)
                .Build();

            var source1Result1 = building.RequestHallCall(floor: 1, direction: Direction.UP, source: "Source1");
            var source1Result2 = building.RequestHallCall(floor: 2, direction: Direction.UP, source: "Source1");
            var source2Result1 = building.RequestHallCall(floor: 3, direction: Direction.UP, source: "Source2");
            var source2Result2 = building.RequestHallCall(floor: 4, direction: Direction.UP, source: "Source2");
            
            Assert.True(source1Result1.IsSuccess);
            Assert.True(source1Result2.IsSuccess);
            Assert.True(source2Result1.IsSuccess);
            Assert.True(source2Result2.IsSuccess);
        }
    }
}
