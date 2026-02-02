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
        private readonly Dictionary<Guid, Request> _requests;

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

            _elevators = InitializeElevators(config.ElevatorCount, config.MaxFloors, config.DoorOpenTicks, config.ElevatorMovementTicks, logger);

            _hallCallQueue = new HallCallQueue();
            _requests = new Dictionary<Guid, Request>();

            _logger.LogInfo($"Initialized: {config.ElevatorCount} elevators, {config.MaxFloors} floors");
        }

        /// <summary>
        /// Requests a complete passenger journey (source floor to destination floor).
        /// This is the primary API for passenger requests.
        /// </summary>
        public Result<Request> RequestPassengerJourney(int sourceFloor, int destinationFloor, string source = "RandomGenerator")
        {
            if (!_rateLimiter.IsAllowed(source))
            {
                RejectRequest(RejectionReason.RateLimit);
                _logger.LogWarning($"Rate limit exceeded for source '{source}'");
                return Result<Request>.Failure("Rate limit exceeded, try again later");
            }

            var validationResult = ValidateJourney(sourceFloor, destinationFloor);
            if (!validationResult.IsValid)
            {
                RejectRequest(RejectionReason.Validation);
                _logger.LogWarning(validationResult.ErrorMessage);
                return Result<Request>.Failure(validationResult.ErrorMessage);
            }

            var direction = destinationFloor > sourceFloor ? Direction.UP : Direction.DOWN;

            lock (_lock)
            {
                _metrics.IncrementTotalRequests();

                var hallCall = _hallCallQueue.FindByFloorAndDirection(sourceFloor, direction);
                
                if (hallCall == null)
                {
                    hallCall = new HallCall(sourceFloor, direction);
                    _hallCallQueue.Add(hallCall);
                    _logger.LogDebug($"HallCall {GetShortId(hallCall.Id)} created: Floor {sourceFloor}, {direction}");
                }
                else
                {
                    _logger.LogDebug($"Reusing HallCall {GetShortId(hallCall.Id)} at Floor {sourceFloor}, {direction}");
                }

                hallCall.AddDestination(destinationFloor);

                var journey = Journey.Of(sourceFloor, destinationFloor);
                var request = new Request(hallCall.Id, journey);
                _requests[request.Id] = request;
                _logger.LogInfo($"[REQUEST] Floor {sourceFloor} → {destinationFloor}");

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
            if (!_rateLimiter.IsAllowed(source))
            {
                RejectRequest(RejectionReason.RateLimit);
                _logger.LogWarning($"Rate limit exceeded for source '{source}'");
                return Result<HallCall>.Failure("Rate limit exceeded, try again later");
            }

            var validationResult = ValidateHallCall(floor, direction);
            if (!validationResult.IsValid)
            {
                RejectRequest(RejectionReason.Validation);
                _logger.LogWarning(validationResult.ErrorMessage);
                return Result<HallCall>.Failure(validationResult.ErrorMessage);
            }

            lock (_lock)
            {
                _metrics.IncrementTotalRequests();

                var existing = _hallCallQueue.FindByFloorAndDirection(floor, direction);
                if (existing != null)
                {
                    _logger.LogInfo($"Duplicate hall call ignored: Floor {floor}, Direction {direction}");
                    _metrics.IncrementAcceptedRequests();
                    return Result<HallCall>.Success(existing);
                }

                if (IsQueueAtCapacity())
                {
                    _logger.LogError("Hall call queue at capacity");
                    RejectRequest(RejectionReason.QueueFull);
                    return Result<HallCall>.Failure("System at capacity, try again later");
                }

                var hallCall = new HallCall(floor, direction);
                _hallCallQueue.Add(hallCall);
                _metrics.IncrementAcceptedRequests();
                _logger.LogDebug($"HallCall {GetShortId(hallCall.Id)} created: Floor {floor}, {direction}");

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
                CompleteRequests();
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
                return new BuildingStatus
                {
                    Elevators = _elevators.Select(e => e.GetStatus()).ToList(),
                    PendingHallCallsCount = _hallCallQueue.GetPendingCount(),
                    Timestamp = DateTime.UtcNow
                };
            }
        }

        private static List<Elevator> InitializeElevators(int elevatorCount, int maxFloors, int doorOpenTicks, int movementTicks, ILogger logger)
        {
            var elevators = new List<Elevator>(elevatorCount);
            for (int elevatorId = 1; elevatorId <= elevatorCount; elevatorId++)
            {
                int? initialFloor = elevatorId <= 2 ? 0 : (elevatorId <= 4 ? maxFloors : (int?)null);
                elevators.Add(new Elevator(elevatorId, maxFloors, doorOpenTicks, movementTicks, logger, initialFloor));
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
            
            _logger.LogInfo($"[ASSIGN] E{bestElevator.Id} → Floor {hallCall.Floor} {hallCall.Direction}");
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
                elevator.SetDirection(hallCall.Direction);
                
                foreach (var destinationFloor in hallCall.GetDestinations())
                {
                    if (destinationFloor != elevator.CurrentFloor)
                    {
                        elevator.AddDestination(destinationFloor);
                        _logger.LogDebug($"Added passenger destination {destinationFloor} to Elevator {elevator.Id} after boarding at floor {elevator.CurrentFloor}");
                    }
                }

                MarkRequestsAsInTransit(hallCall.Id);

                hallCall.MarkAsCompleted();
                elevator.RemoveHallCallId(hallCall.Id);
                _metrics.IncrementCompletedHallCalls();
                _logger.LogDebug($"HallCall {GetShortId(hallCall.Id)} completed by E{elevator.Id} at floor {elevator.CurrentFloor}");
            }
        }

        private void MarkRequestsAsInTransit(Guid hallCallId)
        {
            var requestsForHallCall = _requests.Values
                .Where(r => r.HallCallId == hallCallId && r.Status == RequestStatus.WAITING)
                .ToList();

            foreach (var request in requestsForHallCall)
            {
                request.MarkAsInTransit();
                _logger.LogDebug($"Request {GetShortId(request.Id)} IN_TRANSIT");
            }
        }

        private void CompleteRequests()
        {
            var elevatorsInLoadingState = _elevators.Where(e => e.State == ElevatorState.LOADING);

            foreach (var elevator in elevatorsInLoadingState)
            {
                CompleteRequestsForElevator(elevator);
            }
        }

        private void CompleteRequestsForElevator(Elevator elevator)
        {
            var requestsToComplete = _requests.Values
                .Where(r => r.Status == RequestStatus.IN_TRANSIT && 
                           r.Journey.DestinationFloor == elevator.CurrentFloor)
                .ToList();

            foreach (var request in requestsToComplete)
            {
                request.MarkAsCompleted();
                _metrics.IncrementCompletedRequests();
                _logger.LogInfo($"[COMPLETE] Floor {elevator.CurrentFloor}");
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

        private ValidationResult ValidateFloor(int floor)
        {
            if (floor < 0 || floor > _maxFloors)
            {
                return ValidationResult.Failure($"Floor {floor} out of range [0, {_maxFloors}]");
            }
            return ValidationResult.Success();
        }

        private ValidationResult ValidateJourney(int sourceFloor, int destinationFloor)
        {
            var sourceResult = ValidateFloor(sourceFloor);
            if (!sourceResult.IsValid)
            {
                return ValidationResult.Failure($"Source {sourceResult.ErrorMessage}");
            }

            var destResult = ValidateFloor(destinationFloor);
            if (!destResult.IsValid)
            {
                return ValidationResult.Failure($"Destination {destResult.ErrorMessage}");
            }

            if (sourceFloor == destinationFloor)
            {
                return ValidationResult.Failure("Source and destination cannot be the same");
            }

            return ValidationResult.Success();
        }

        private ValidationResult ValidateHallCall(int floor, Direction direction)
        {
            var floorResult = ValidateFloor(floor);
            if (!floorResult.IsValid)
            {
                return floorResult;
            }

            if (direction != Direction.UP && direction != Direction.DOWN)
            {
                return ValidationResult.Failure($"Invalid direction: {direction}");
            }

            return ValidationResult.Success();
        }

        private void RejectRequest(RejectionReason reason = RejectionReason.Validation)
        {
            lock (_lock)
            {
                _metrics.IncrementTotalRequests();
                _metrics.IncrementRejectedRequests();

                if (reason == RejectionReason.RateLimit)
                {
                    _metrics.IncrementRateLimitHits();
                }
                else if (reason == RejectionReason.QueueFull)
                {
                    _metrics.IncrementQueueFullRejections();
                }
            }
        }

        private static string GetShortId(Guid id)
        {
            return id.ToString().Substring(0, 8);
        }

        private enum RejectionReason
        {
            Validation,
            RateLimit,
            QueueFull
        }

        private class ValidationResult
        {
            public bool IsValid { get; private set; }
            public string ErrorMessage { get; private set; }

            private ValidationResult(bool isValid, string errorMessage)
            {
                IsValid = isValid;
                ErrorMessage = errorMessage;
            }

            public static ValidationResult Success() => new ValidationResult(true, string.Empty);
            public static ValidationResult Failure(string errorMessage) => new ValidationResult(false, errorMessage);
        }
    }
}
