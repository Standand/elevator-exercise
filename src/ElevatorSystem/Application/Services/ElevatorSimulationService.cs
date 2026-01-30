using System;
using System.Linq;
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
        private int _tickCount = 0;
        private const int STATUS_LOG_INTERVAL_TICKS = 10;

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
                    _tickCount++;
                    
                    if (_tickCount >= STATUS_LOG_INTERVAL_TICKS)
                    {
                        LogElevatorStatuses();
                        _tickCount = 0;
                    }
                    
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
                throw;
            }

            _logger.LogInfo("Simulation stopped");
        }

        /// <summary>
        /// Logs the current status of all elevators.
        /// </summary>
        private void LogElevatorStatuses()
        {
            var status = _building.GetStatus();
            var elevatorStatuses = status.Elevators
                .OrderBy(e => e.Id)
                .Select(e =>
                {
                    var destStr = e.Destinations.Count > 0 
                        ? $"[{string.Join(", ", e.Destinations)}]" 
                        : "[]";
                    return $"Elevator {e.Id}: Floor {e.CurrentFloor}, {e.State}, {e.Direction}, Destinations: {destStr}";
                })
                .ToList();
            
            var compactStatuses = status.Elevators
                .OrderBy(e => e.Id)
                .Select(e => $"E{e.Id}:F{e.CurrentFloor} {e.State} {e.Direction} D:[{string.Join(",", e.Destinations)}]")
                .ToList();
            _logger.LogInfo($"[STATUS] {string.Join(" | ", compactStatuses)}");
        }
    }
}
