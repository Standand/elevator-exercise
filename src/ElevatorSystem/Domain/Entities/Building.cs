using System;
using System.Collections.Generic;
using System.Linq;
using ElevatorSystem.Common;
using ElevatorSystem.Domain.Services;
using ElevatorSystem.Domain.ValueObjects;
using ElevatorSystem.Infrastructure.Configuration;
using ElevatorSystem.Infrastructure.Logging;
using ElevatorSystem.Infrastructure.Metrics;

namespace ElevatorSystem.Domain.Entities
{
    /// <summary>
    /// Represents a building with multiple elevators.
    /// Aggregate root that coordinates elevator operations.
    /// </summary>
    public class Building
    {
        // Dependencies (injected)
        private readonly ISchedulingStrategy _schedulingStrategy;
        private readonly ILogger _logger;
        private readonly IMetrics _metrics;
        private readonly RateLimiter _rateLimiter;

        // Configuration
        private readonly int _maxFloors;

        // State (protected by lock)
        private readonly object _lock = new object();
        private readonly List<Elevator> _elevators;
        private readonly HallCallQueue _hallCallQueue;

        public Building(
            ISchedulingStrategy schedulingStrategy,
            ILogger logger,
            IMetrics metrics,
            RateLimiter rateLimiter,
            SimulationConfiguration config)
        {
            _schedulingStrategy = schedulingStrategy ?? throw new ArgumentNullException(nameof(schedulingStrategy));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));

            _maxFloors = config.MaxFloors;

            // Initialize elevators
            _elevators = new List<Elevator>();
            for (int i = 1; i <= config.ElevatorCount; i++)
            {
                _elevators.Add(new Elevator(i, config.MaxFloors, config.DoorOpenTicks, logger));
            }

            _hallCallQueue = new HallCallQueue();

            _logger.LogInfo($"Building initialized: {config.ElevatorCount} elevators, {config.MaxFloors} floors");
        }

        /// <summary>
        /// Requests a hall call (button press at a floor).
        /// </summary>
        public Result<HallCall> RequestHallCall(int floor, Direction direction, string source = "RandomGenerator")
        {
            lock (_lock)
            {
                _metrics.IncrementTotalRequests();

                // 1. Rate limiting
                if (!_rateLimiter.IsAllowed(source))
                {
                    _logger.LogWarning($"Rate limit exceeded for source '{source}'");
                    _metrics.IncrementRateLimitHits();
                    _metrics.IncrementRejectedRequests();
                    return Result<HallCall>.Failure("Rate limit exceeded, try again later");
                }

                // 2. Validation
                if (floor < 0 || floor > _maxFloors)
                {
                    _logger.LogWarning($"Invalid floor: {floor}");
                    _metrics.IncrementRejectedRequests();
                    return Result<HallCall>.Failure($"Floor {floor} out of range [0, {_maxFloors}]");
                }

                if (direction != Direction.UP && direction != Direction.DOWN)
                {
                    _logger.LogWarning($"Invalid direction: {direction}");
                    _metrics.IncrementRejectedRequests();
                    return Result<HallCall>.Failure($"Invalid direction: {direction}");
                }

                // 3. Idempotency check
                var existing = _hallCallQueue.FindByFloorAndDirection(floor, direction);
                if (existing != null)
                {
                    _logger.LogInfo($"Duplicate hall call ignored: Floor {floor}, Direction {direction}");
                    _metrics.IncrementAcceptedRequests();
                    return Result<HallCall>.Success(existing);
                }

                // 4. Capacity check
                if (_hallCallQueue.GetPendingCount() >= _maxFloors * 2 - 2)
                {
                    _logger.LogError("Hall call queue at capacity");
                    _metrics.IncrementQueueFullRejections();
                    _metrics.IncrementRejectedRequests();
                    return Result<HallCall>.Failure("System at capacity, try again later");
                }

                // 5. Create hall call
                var hallCall = new HallCall(floor, direction);
                _hallCallQueue.Add(hallCall);
                _metrics.IncrementAcceptedRequests();
                _logger.LogInfo($"HallCall {hallCall.Id} created: Floor {floor}, Direction {direction}");

                return Result<HallCall>.Success(hallCall);
            }
        }

        /// <summary>
        /// Processes one simulation tick.
        /// </summary>
        public void ProcessTick()
        {
            lock (_lock)
            {
                // Step 1: Retry pending hall calls (FIFO order)
                AssignPendingHallCalls();

                // Step 2: Process each elevator (fixed order: 1, 2, 3, 4)
                foreach (var elevator in _elevators.OrderBy(e => e.Id))
                {
                    elevator.ProcessTick();
                }

                // Step 3: Complete hall calls at elevator floors
                CompleteHallCalls();

                // Step 4: Update metrics
                _metrics.SetPendingHallCallsCount(_hallCallQueue.GetPendingCount());
                _metrics.SetActiveElevatorsCount(_elevators.Count(e => e.State != ElevatorState.IDLE));
            }
        }

        /// <summary>
        /// Gets the current status of the building.
        /// </summary>
        public BuildingStatus GetStatus()
        {
            lock (_lock)
            {
                return new BuildingStatus(
                    _elevators.Select(e => e.GetStatus()).ToList(),
                    _hallCallQueue.GetPendingCount(),
                    DateTime.UtcNow);
            }
        }

        // Private helper methods (assume lock is held)

        private void AssignPendingHallCalls()
        {
            var pendingHallCalls = _hallCallQueue.GetPending()
                                                  .OrderBy(hc => hc.CreatedAt)
                                                  .ToList();

            foreach (var hallCall in pendingHallCalls)
            {
                var elevator = _schedulingStrategy.SelectBestElevator(hallCall, _elevators);

                if (elevator != null)
                {
                    elevator.AssignHallCall(hallCall);
                    hallCall.MarkAsAssigned(elevator.Id);
                    _logger.LogInfo($"HallCall {hallCall.Id} assigned to Elevator {elevator.Id}");
                }
                else
                {
                    _logger.LogDebug($"No elevator available for HallCall {hallCall.Id}, will retry");
                }
            }
        }

        private void CompleteHallCalls()
        {
            // Check each elevator in LOADING state
            foreach (var elevator in _elevators.Where(e => e.State == ElevatorState.LOADING))
            {
                // Find hall calls assigned to this elevator at its current floor
                var hallCallsToComplete = _hallCallQueue.GetAll()
                    .Where(hc =>
                        hc.Status == HallCallStatus.ASSIGNED &&
                        hc.AssignedElevatorId == elevator.Id &&
                        hc.Floor == elevator.CurrentFloor)
                    .ToList();

                foreach (var hallCall in hallCallsToComplete)
                {
                    hallCall.MarkAsCompleted();
                    elevator.RemoveHallCallId(hallCall.Id);
                    _metrics.IncrementCompletedHallCalls();
                    _logger.LogInfo($"HallCall {hallCall.Id} completed by Elevator {elevator.Id} at floor {elevator.CurrentFloor}");
                }
            }
        }
    }
}
