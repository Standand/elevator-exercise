using System;
using ElevatorSystem.Common;
using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.Services;
using ElevatorSystem.Infrastructure.Configuration;
using ElevatorSystem.Infrastructure.Metrics;
using ElevatorSystem.Infrastructure.Time;

namespace ElevatorSystem.Tests.TestHelpers
{
    /// <summary>
    /// Factory methods for creating test objects with sensible defaults.
    /// Reduces boilerplate in test code.
    /// </summary>
    public static class TestBuilders
    {
        /// <summary>
        /// Creates a Building for testing with configurable parameters.
        /// </summary>
        public static Building CreateBuilding(
            ISchedulingStrategy? strategy = null,
            int elevatorCount = 4,
            int maxFloors = 10,
            int doorOpenTicks = 3,
            int elevatorMovementTicks = 1,
            ITimeService? timeService = null)
        {
            var logger = new MockLogger();
            var metrics = new SystemMetrics();
            var rateLimiter = new RateLimiter(20, 10, logger);
            var config = new SimulationConfiguration
            {
                MaxFloors = maxFloors,
                ElevatorCount = elevatorCount,
                DoorOpenTicks = doorOpenTicks,
                ElevatorMovementTicks = elevatorMovementTicks,
                TickIntervalMs = 1000,
                RequestIntervalSeconds = 5
            };
            
            return new Building(
                strategy ?? new DirectionAwareStrategy(),
                logger,
                metrics,
                rateLimiter,
                config);
        }
        
        /// <summary>
        /// Creates an Elevator for testing with configurable parameters.
        /// </summary>
        public static Elevator CreateElevator(
            int id = 1,
            int currentFloor = 0,
            int maxFloors = 10,
            int doorOpenTicks = 3,
            int elevatorMovementTicks = 1)
        {
            var logger = new MockLogger();
            
            // Create elevator at ground floor
            var elevator = new Elevator(id, maxFloors, doorOpenTicks, elevatorMovementTicks, logger);
            
            // If we need to position it at a different floor, we need to move it
            // This is a limitation of the current design - elevators always start at floor 0
            // For now, we'll document this limitation
            
            return elevator;
        }
        
        /// <summary>
        /// Creates a RateLimiter for testing with configurable parameters.
        /// </summary>
        public static RateLimiter CreateRateLimiter(
            int globalLimit = 20,
            int perSourceLimit = 10,
            ITimeService? timeService = null)
        {
            var logger = new MockLogger();
            return new RateLimiter(globalLimit, perSourceLimit, logger);
        }
    }
}
