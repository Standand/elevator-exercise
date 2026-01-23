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
    /// Thread-safe: All public methods use pessimistic locking.
    /// </summary>
    public class Building
    {
        private const int QUEUE_CAPACITY_MULTIPLIER = 2;
        private const int QUEUE_CAPACITY_OFFSET = 2;
        
        private readonly ISchedulingStrategy _schedulingStrategy;
        private readonly ILogger _logger;
        private readonly IMetrics _metrics;
        private readonly RateLimiter _rateLimiter;
        private readonly int _maxFloors;
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

            _elevators = InitializeElevators(config.ElevatorCount, config.MaxFloors, config.DoorOpenTicks, logger);

            _hallCallQueue = new HallCallQueue();

            _logger.LogInfo($"Building initialized: {config.ElevatorCount} elevators, {config.MaxFloors} floors");
        }

        /// <summary>
        /// Requests a complete passenger journey (source floor to destination floor).
        /// This is the primary API for passenger requests.
        /// </summary>
        public Result<Request> RequestPassengerJourney(int sourceFloor, int destinationFloor, string source = "RandomGenerator")
        {
            // 1. Rate limiting (outside lock - RateLimiter is thread-safe)
            if (!_rateLimiter.IsAllowed(source))
            {
                // Need lock for metrics only
                lock (_lock)
                {
                    _metrics.IncrementTotalRequests();
                    _metrics.IncrementRateLimitHits();
                    _metrics.IncrementRejectedRequests();
                }
                _logger.LogWarning($"Rate limit exceeded for source '{source}'");
                return Result<Request>.Failure("Rate limit exceeded, try again later");
            }

            // 2. Validation (outside lock - only reads immutable _maxFloors)
            if (sourceFloor < 0 || sourceFloor > _maxFloors)
            {
                lock (_lock)
                {
                    _metrics.IncrementTotalRequests();
                    _metrics.IncrementRejectedRequests();
                }
                _logger.LogWarning($"Invalid source floor: {sourceFloor}");
                return Result<Request>.Failure($"Source floor {sourceFloor} out of range [0, {_maxFloors}]");
            }

            if (destinationFloor < 0 || destinationFloor > _maxFloors)
            {
                lock (_lock)
                {
                    _metrics.IncrementTotalRequests();
                    _metrics.IncrementRejectedRequests();
                }
                _logger.LogWarning($"Invalid destination floor: {destinationFloor}");
                return Result<Request>.Failure($"Destination floor {destinationFloor} out of range [0, {_maxFloors}]");
            }

            if (sourceFloor == destinationFloor)
            {
                lock (_lock)
                {
                    _metrics.IncrementTotalRequests();
                    _metrics.IncrementRejectedRequests();
                }
                _logger.LogWarning($"Source and destination are the same: {sourceFloor}");
                return Result<Request>.Failure("Source and destination cannot be the same");
            }

            // 3. Determine direction (outside lock - pure calculation)
            var direction = destinationFloor > sourceFloor ? Direction.UP : Direction.DOWN;

            // 4. Acquire lock only when accessing shared state
            lock (_lock)
            {
                _metrics.IncrementTotalRequests();

                // 5. Find or create hall call (accesses shared _hallCallQueue)
                var hallCall = _hallCallQueue.FindByFloorAndDirection(sourceFloor, direction);
                
                if (hallCall == null)
                {
                    // Create new hall call
                    hallCall = new HallCall(sourceFloor, direction);
                    _hallCallQueue.Add(hallCall);
                    _logger.LogInfo($"HallCall {hallCall.Id} created: Floor {sourceFloor}, Direction {direction}");
                }
                else
                {
                    _logger.LogInfo($"Reusing existing HallCall {hallCall.Id} at Floor {sourceFloor}, Direction {direction}");
                }

                // 6. Add destination to hall call
                hallCall.AddDestination(destinationFloor);

                // 7. Create passenger request
                var journey = Journey.Of(sourceFloor, destinationFloor);
                var request = new Request(hallCall.Id, journey);
                _logger.LogInfo($"Request {request.Id} created: {sourceFloor} → {destinationFloor}");

                // 8. If hall call is already assigned, add destination to elevator
                if (hallCall.Status == HallCallStatus.ASSIGNED && hallCall.AssignedElevatorId.HasValue)
                {
                    var elevator = _elevators.FirstOrDefault(e => e.Id == hallCall.AssignedElevatorId.Value);
                    if (elevator != null)
                    {
                        elevator.AddDestination(destinationFloor);
                        _logger.LogInfo($"Added destination {destinationFloor} to Elevator {elevator.Id}");
                    }
                }

                _metrics.IncrementAcceptedRequests();
                return Result<Request>.Success(request);
            }
        }

        /// <summary>
        /// Requests a hall call (button press at a floor).
        /// Legacy method - prefer RequestPassengerJourney for complete journeys.
        /// </summary>
        public Result<HallCall> RequestHallCall(int floor, Direction direction, string source = "RandomGenerator")
        {
            // 1. Rate limiting (outside lock - RateLimiter is thread-safe)
            if (!_rateLimiter.IsAllowed(source))
            {
                // Need lock for metrics only
                lock (_lock)
                {
                    _metrics.IncrementTotalRequests();
                    _metrics.IncrementRateLimitHits();
                    _metrics.IncrementRejectedRequests();
                }
                _logger.LogWarning($"Rate limit exceeded for source '{source}'");
                return Result<HallCall>.Failure("Rate limit exceeded, try again later");
            }

            // 2. Validation (outside lock - only reads immutable _maxFloors)
            if (floor < 0 || floor > _maxFloors)
            {
                lock (_lock)
                {
                    _metrics.IncrementTotalRequests();
                    _metrics.IncrementRejectedRequests();
                }
                _logger.LogWarning($"Invalid floor: {floor}");
                return Result<HallCall>.Failure($"Floor {floor} out of range [0, {_maxFloors}]");
            }

            if (direction != Direction.UP && direction != Direction.DOWN)
            {
                lock (_lock)
                {
                    _metrics.IncrementTotalRequests();
                    _metrics.IncrementRejectedRequests();
                }
                _logger.LogWarning($"Invalid direction: {direction}");
                return Result<HallCall>.Failure($"Invalid direction: {direction}");
            }

            // 3. Acquire lock only when accessing shared state
            lock (_lock)
            {
                _metrics.IncrementTotalRequests();

                // 4. Idempotency check (accesses shared _hallCallQueue)
                var existing = _hallCallQueue.FindByFloorAndDirection(floor, direction);
                if (existing != null)
                {
                    _logger.LogInfo($"Duplicate hall call ignored: Floor {floor}, Direction {direction}");
                    _metrics.IncrementAcceptedRequests();
                    return Result<HallCall>.Success(existing);
                }

                // 5. Capacity check (accesses shared _hallCallQueue)
                if (IsQueueAtCapacity())
                {
                    _logger.LogError("Hall call queue at capacity");
                    _metrics.IncrementQueueFullRejections();
                    _metrics.IncrementRejectedRequests();
                    return Result<HallCall>.Failure("System at capacity, try again later");
                }

                // 6. Create hall call (modifies shared _hallCallQueue)
                var hallCall = new HallCall(floor, direction);
                _hallCallQueue.Add(hallCall);
                _metrics.IncrementAcceptedRequests();
                _logger.LogInfo($"HallCall {hallCall.Id} created: Floor {floor}, Direction {direction}");

                return Result<HallCall>.Success(hallCall);
            }
        }

        /// <summary>
        /// Processes one simulation tick.
        /// Thread-safe: Protected by internal lock.
        /// </summary>
        public void ProcessTick()
        {
            lock (_lock)
            {
                AssignPendingHallCalls();
                ProcessAllElevators();
                CompleteHallCalls();
                UpdateMetrics();
            }
        }

        /// <summary>
        /// Gets the current status of the building.
        /// Thread-safe: Protected by internal lock.
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

        private static List<Elevator> InitializeElevators(int elevatorCount, int maxFloors, int doorOpenTicks, ILogger logger)
        {
            var elevators = new List<Elevator>(elevatorCount);
            for (int elevatorId = 1; elevatorId <= elevatorCount; elevatorId++)
            {
                elevators.Add(new Elevator(elevatorId, maxFloors, doorOpenTicks, logger));
            }
            return elevators;
        }

        private bool IsQueueAtCapacity()
        {
            int queueCapacity = _maxFloors * QUEUE_CAPACITY_MULTIPLIER - QUEUE_CAPACITY_OFFSET;
            return _hallCallQueue.GetPendingCount() >= queueCapacity;
        }

        private void ProcessAllElevators()
        {
            foreach (var elevator in _elevators.OrderBy(e => e.Id))
            {
                elevator.ProcessTick();
            }
        }

        private void UpdateMetrics()
        {
            int pendingCount = _hallCallQueue.GetPendingCount();
            int activeCount = _elevators.Count(e => e.State != ElevatorState.IDLE);
            
            _metrics.SetPendingHallCallsCount(pendingCount);
            _metrics.SetActiveElevatorsCount(activeCount);
        }

        private void AssignPendingHallCalls()
        {
            var pendingHallCallsInFifoOrder = _hallCallQueue.GetPending()
                                                            .OrderBy(hc => hc.CreatedAt)
                                                            .ToList();

            foreach (var hallCall in pendingHallCallsInFifoOrder)
            {
                TryAssignHallCallToElevator(hallCall);
            }
        }

        private void TryAssignHallCallToElevator(HallCall hallCall)
        {
            var bestElevator = _schedulingStrategy.SelectBestElevator(hallCall, _elevators);

            if (bestElevator == null)
            {
                _logger.LogDebug($"No elevator available for HallCall {hallCall.Id}, will retry");
                return;
            }

            bestElevator.AssignHallCall(hallCall);
            hallCall.MarkAsAssigned(bestElevator.Id);
            AddDestinationsToElevator(bestElevator, hallCall);
            
            _logger.LogInfo($"HallCall {hallCall.Id} assigned to Elevator {bestElevator.Id} with {hallCall.GetDestinations().Count} destination(s)");
        }

        private void AddDestinationsToElevator(Elevator elevator, HallCall hallCall)
        {
            foreach (var destinationFloor in hallCall.GetDestinations())
            {
                elevator.AddDestination(destinationFloor);
                _logger.LogDebug($"Added destination {destinationFloor} to Elevator {elevator.Id}");
            }
        }

        private void CompleteHallCalls()
        {
            var elevatorsInLoadingState = _elevators.Where(e => e.State == ElevatorState.LOADING);

            foreach (var elevator in elevatorsInLoadingState)
            {
                CompleteHallCallsForElevator(elevator);
            }
        }

        private void CompleteHallCallsForElevator(Elevator elevator)
        {
            var hallCallsAtCurrentFloor = FindHallCallsAssignedToElevatorAtFloor(elevator);

            foreach (var hallCall in hallCallsAtCurrentFloor)
            {
                hallCall.MarkAsCompleted();
                elevator.RemoveHallCallId(hallCall.Id);
                _metrics.IncrementCompletedHallCalls();
                _logger.LogInfo($"HallCall {hallCall.Id} completed by Elevator {elevator.Id} at floor {elevator.CurrentFloor}");
            }
        }

        private List<HallCall> FindHallCallsAssignedToElevatorAtFloor(Elevator elevator)
        {
            return _hallCallQueue.GetAll()
                .Where(hc =>
                    hc.Status == HallCallStatus.ASSIGNED &&
                    hc.AssignedElevatorId == elevator.Id &&
                    hc.Floor == elevator.CurrentFloor)
                .ToList();
        }
    }
}
