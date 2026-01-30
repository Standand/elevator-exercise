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
            Console.WriteLine("=== Elevator Control System Simulation ===\n");

            var config = ConfigurationLoader.Load("appsettings.json");
            Console.WriteLine($"Configuration loaded: {config.ElevatorCount} elevators, {config.MaxFloors} floors\n");

            // Use Serilog for structured logging with class context
            var logger = new ConsoleLogger(enableDebug: false, className: "Program");
            var timeService = new SystemTimeService();
            var metrics = new SystemMetrics();
            var rateLimiter = new RateLimiter(
                globalLimitPerMinute: 20,
                perSourceLimitPerMinute: 10,
                logger);

            var schedulingStrategy = new DirectionAwareStrategy();

            var building = new Building(
                schedulingStrategy,
                logger,
                metrics,
                rateLimiter,
                config);

            var simulationService = new ElevatorSimulationService(
                building,
                timeService,
                logger,
                config);

            var requestGenerator = new RandomRequestGenerator(
                building,
                logger,
                config);

            var orchestrator = new SystemOrchestrator(
                simulationService,
                requestGenerator,
                metrics,
                logger);

            var cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                Console.WriteLine("\n\nShutdown requested (Ctrl+C)...");
                orchestrator.Shutdown();
                e.Cancel = true;
            };

            Console.WriteLine("System started. Press Ctrl+C to stop.\n");

            try
            {
                await orchestrator.StartAsync(cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Waiting for tasks to complete...");
                await Task.Delay(1000);
            }

            Console.WriteLine("\nSystem stopped.");
        }
    }
}
