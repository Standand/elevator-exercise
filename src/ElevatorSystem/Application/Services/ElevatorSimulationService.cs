using System;
using System.Threading;
using System.Threading.Tasks;
using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Infrastructure.Configuration;
using ElevatorSystem.Infrastructure.Logging;
using ElevatorSystem.Infrastructure.Time;

namespace ElevatorSystem.Application.Services
{
    /// <summary>
    /// Service that runs the elevator simulation loop.
    /// </summary>
    public class ElevatorSimulationService
    {
        private readonly Building _building;
        private readonly ITimeService _timeService;
        private readonly ILogger _logger;
        private readonly int _tickIntervalMs;

        public ElevatorSimulationService(
            Building building,
            ITimeService timeService,
            ILogger logger,
            SimulationConfiguration config)
        {
            _building = building ?? throw new ArgumentNullException(nameof(building));
            _timeService = timeService ?? throw new ArgumentNullException(nameof(timeService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _tickIntervalMs = config.TickIntervalMs;
        }

        /// <summary>
        /// Runs the simulation loop until cancellation is requested.
        /// </summary>
        public async Task RunAsync(CancellationToken cancellationToken)
        {
            _logger.LogInfo("Simulation started");

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    _building.ProcessTick();
                    await Task.Delay(_tickIntervalMs, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInfo("Simulation cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError($"FATAL: Unexpected exception in simulation loop: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                throw;
            }

            _logger.LogInfo("Simulation stopped");
        }
    }
}
