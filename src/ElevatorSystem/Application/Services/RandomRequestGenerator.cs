using System;
using System.Threading;
using System.Threading.Tasks;
using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.ValueObjects;
using ElevatorSystem.Infrastructure.Configuration;
using ElevatorSystem.Infrastructure.Logging;

namespace ElevatorSystem.Application.Services
{
    /// <summary>
    /// Service that generates random hall call requests.
    /// </summary>
    public class RandomRequestGenerator
    {
        private readonly Building _building;
        private readonly ILogger _logger;
        private readonly int _requestIntervalMs;
        private readonly int _maxFloors;
        private readonly Random _random;

        public RandomRequestGenerator(
            Building building,
            ILogger logger,
            SimulationConfiguration config)
        {
            _building = building ?? throw new ArgumentNullException(nameof(building));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _requestIntervalMs = config.RequestIntervalSeconds * 1000;
            _maxFloors = config.MaxFloors;
            _random = new Random();
        }

        /// <summary>
        /// Runs the request generator loop until cancellation is requested.
        /// </summary>
        public async Task RunAsync(CancellationToken cancellationToken)
        {
            _logger.LogInfo("Request generator started");

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Generate random request
                    var floor = _random.Next(0, _maxFloors + 1);
                    var direction = _random.Next(2) == 0 ? Direction.UP : Direction.DOWN;

                    var result = _building.RequestHallCall(floor, direction, "RandomGenerator");

                    if (result.IsSuccess)
                    {
                        _logger.LogInfo($"Generated request: Floor {floor}, Direction {direction}");
                    }
                    else
                    {
                        _logger.LogWarning($"Request rejected: {result.Error}");
                    }

                    // Wait for next request
                    await Task.Delay(_requestIntervalMs, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown
                _logger.LogInfo("Request generator cancelled");
            }
            catch (Exception ex)
            {
                // Log and continue
                _logger.LogError($"Error generating request: {ex.Message}");
                _logger.LogDebug($"Stack trace: {ex.StackTrace}");

                // Wait before retrying
                await Task.Delay(1000, cancellationToken);
            }

            _logger.LogInfo("Request generator stopped");
        }
    }
}
