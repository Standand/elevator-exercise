using System;
using System.Threading;
using System.Threading.Tasks;
using ElevatorSystem.Application.Services;
using ElevatorSystem.Common;
using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.Services;
using ElevatorSystem.Infrastructure.Configuration;
using ElevatorSystem.Infrastructure.Logging;
using ElevatorSystem.Infrastructure.Metrics;
using ElevatorSystem.Infrastructure.Time;

namespace ElevatorSystem
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("=== Elevator Control System ===\n");

            // Step 1: Load configuration
            var config = ConfigurationLoader.Load("appsettings.json");
            Console.WriteLine($"Configuration loaded: {config.ElevatorCount} elevators, {config.MaxFloors} floors\n");

            // Step 2: Create infrastructure dependencies
            var logger = new ConsoleLogger(enableDebug: false);
            var timeService = new SystemTimeService();
            var metrics = new SystemMetrics();
            var rateLimiter = new RateLimiter(
                globalLimitPerMinute: 20,
                perSourceLimitPerMinute: 10,
                logger);

            // Step 3: Create domain services
            var schedulingStrategy = new DirectionAwareStrategy();

            // Step 4: Create building (domain entity)
            var building = new Building(
                schedulingStrategy,
                logger,
                metrics,
                rateLimiter,
                config);

            // Step 5: Create application services
            var simulationService = new ElevatorSimulationService(
                building,
                timeService,
                logger,
                config);

            var requestGenerator = new RandomRequestGenerator(
                building,
                logger,
                config);

            // Step 6: Create orchestrator
            var orchestrator = new SystemOrchestrator(
                simulationService,
                requestGenerator,
                metrics,
                logger);

            // Step 7: Setup graceful shutdown
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                Console.WriteLine("\n\nShutdown requested (Ctrl+C)...");
                orchestrator.Shutdown();
                cts.Cancel();
                e.Cancel = true;  // Prevent immediate termination
            };

            // Step 8: Start system
            Console.WriteLine("System started. Press Ctrl+C to stop.\n");

            try
            {
                await orchestrator.StartAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected on Ctrl+C
            }

            Console.WriteLine("\nSystem stopped.");
        }
    }
}
