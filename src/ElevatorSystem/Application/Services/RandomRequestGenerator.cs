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
                    var sourceFloor = _random.Next(0, _maxFloors + 1);
                    var destinationFloor = GenerateDifferentDestinationFloor(sourceFloor);

                    var result = _building.RequestPassengerJourney(sourceFloor, destinationFloor, "RandomGenerator");

                    if (result.IsSuccess)
                    {
                        var request = result.Value!;
                        var direction = destinationFloor > sourceFloor ? "UP" : "DOWN";
                        _logger.LogInfo($"Generated passenger journey: Floor {sourceFloor} → {destinationFloor} ({direction}) [Request: {request.Id}]");
                    }
                    else
                    {
                        _logger.LogWarning($"Passenger journey rejected: {result.Error}");
                    }

                    await Task.Delay(_requestIntervalMs, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInfo("Request generator cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error generating request: {ex.Message}");
                _logger.LogDebug($"Stack trace: {ex.StackTrace}");
                await Task.Delay(1000, cancellationToken);
            }

            _logger.LogInfo("Request generator stopped");
        }

        private int GenerateDifferentDestinationFloor(int sourceFloor)
        {
            var destinationFloor = _random.Next(0, _maxFloors + 1);
            while (destinationFloor == sourceFloor)
            {
                destinationFloor = _random.Next(0, _maxFloors + 1);
            }
            return destinationFloor;
        }
    }
}
