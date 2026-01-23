using ElevatorSystem.Common;
using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.Services;
using ElevatorSystem.Domain.ValueObjects;
using ElevatorSystem.Infrastructure.Configuration;
using ElevatorSystem.Infrastructure.Logging;
using ElevatorSystem.Infrastructure.Metrics;
using ElevatorSystem.Tests.TestHelpers;

namespace ElevatorSystem.Tests.Integration.TestHelpers
{
    /// <summary>
    /// Builder for creating integration test scenarios.
    /// </summary>
    public class IntegrationTestBuilder
    {
        private ISchedulingStrategy? _schedulingStrategy;
        private ILogger? _logger;
        private IMetrics? _metrics;
        private RateLimiter? _rateLimiter;
        private SimulationConfiguration? _config;

        public IntegrationTestBuilder WithSchedulingStrategy(ISchedulingStrategy strategy)
        {
            _schedulingStrategy = strategy;
            return this;
        }

        public IntegrationTestBuilder WithLogger(ILogger logger)
        {
            _logger = logger;
            return this;
        }

        public IntegrationTestBuilder WithMetrics(IMetrics metrics)
        {
            _metrics = metrics;
            return this;
        }

        public IntegrationTestBuilder WithRateLimiter(RateLimiter rateLimiter)
        {
            _rateLimiter = rateLimiter;
            return this;
        }

        public IntegrationTestBuilder WithConfiguration(SimulationConfiguration config)
        {
            _config = config;
            return this;
        }

        public Building Build()
        {
            var logger = _logger ?? new MockLogger();
            var metrics = _metrics ?? new SystemMetrics();
            var rateLimiter = _rateLimiter ?? new RateLimiter(100, 50, logger);
            var config = _config ?? new SimulationConfiguration
            {
                MaxFloors = 10,
                ElevatorCount = 4,
                TickIntervalMs = 1000,
                DoorOpenTicks = 3,
                ElevatorMovementTicks = 3,
                RequestIntervalSeconds = 5
            };
            var strategy = _schedulingStrategy ?? new DirectionAwareStrategy();

            return new Building(strategy, logger, metrics, rateLimiter, config);
        }

        public static IntegrationTestBuilder Create()
        {
            return new IntegrationTestBuilder();
        }
    }
}
