using System;
using System.Collections.Generic;
using System.Linq;
using ElevatorSystem.Domain.ValueObjects;
using ElevatorSystem.Infrastructure.Logging;

namespace ElevatorSystem.Domain.Entities
{
    /// <summary>
    /// Represents an elevator car with its state and behavior.
    /// Entity with identity (Id).
    /// </summary>
    public class Elevator
    {
        // Identity
        public int Id { get; }

        // Dependencies
        private readonly ILogger _logger;

        // Configuration
        private readonly int _maxFloors;
        private readonly int _doorOpenDuration;

        // State (mutable, but protected by Building's lock)
        public int CurrentFloor { get; private set; }
        public Direction Direction { get; private set; }
        public ElevatorState State { get; private set; }

        private readonly DestinationSet _destinations;
        private readonly List<Guid> _assignedHallCallIds;
        private int _doorTimer;
        private int _loadingStateTickCount;

        private const int SAFETY_TIMEOUT_TICKS = 10;

        public Elevator(int id, int maxFloors, int doorOpenDuration, ILogger logger)
        {
            Id = id;
            _maxFloors = maxFloors;
            _doorOpenDuration = doorOpenDuration;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Initial state
            CurrentFloor = 0;
            Direction = ValueObjects.Direction.IDLE;
            State = ElevatorState.IDLE;
            _destinations = new DestinationSet(ValueObjects.Direction.IDLE);
            _assignedHallCallIds = new List<Guid>();
            _doorTimer = 0;
            _loadingStateTickCount = 0;
        }

        /// <summary>
        /// Checks if this elevator can accept a hall call.
        /// </summary>
        public bool CanAcceptHallCall(HallCall hallCall)
        {
            // Rule 1: If at the hall call floor in LOADING state, CANNOT accept
            // (Elevator already there, don't accept duplicate calls)
            if (CurrentFloor == hallCall.Floor &&
                Direction == hallCall.Direction &&
                State == ElevatorState.LOADING)
            {
                return false; // Already servicing this floor
            }

            // Rule 2: If IDLE, can accept any hall call
            if (State == ElevatorState.IDLE)
                return true;

            // Rule 3: If MOVING, must be same direction
            if (Direction != hallCall.Direction)
                return false;

            // Rule 4: Hall call floor must be between current and furthest destination
            // AND not equal to current floor (already checked above)
            if (_destinations.IsEmpty)
                return false;

            var furthestDestination = _destinations.GetFurthestDestination();

            if (Direction == ValueObjects.Direction.UP)
            {
                return CurrentFloor < hallCall.Floor &&
                       hallCall.Floor <= furthestDestination;
            }
            else // DOWN
            {
                return CurrentFloor > hallCall.Floor &&
                       hallCall.Floor >= furthestDestination;
            }
        }

        /// <summary>
        /// Assigns a hall call to this elevator.
        /// </summary>
        public void AssignHallCall(HallCall hallCall)
        {
            _assignedHallCallIds.Add(hallCall.Id);
            _destinations.Add(hallCall.Floor);
            _logger.LogInfo($"Elevator {Id} assigned HallCall {hallCall.Id} (Floor {hallCall.Floor})");
        }

        /// <summary>
        /// Adds a destination floor (for passenger requests).
        /// </summary>
        public void AddDestination(int floor)
        {
            _destinations.Add(floor);
            _logger.LogInfo($"Elevator {Id} destination added: Floor {floor}");
        }

        /// <summary>
        /// Processes one simulation tick.
        /// </summary>
        public void ProcessTick()
        {
            if (State == ElevatorState.IDLE)
            {
                ProcessIdleState();
            }
            else if (State == ElevatorState.MOVING)
            {
                ProcessMovingState();
            }
            else if (State == ElevatorState.LOADING)
            {
                ProcessLoadingState();
            }
        }

        private void ProcessIdleState()
        {
            // If destinations exist, transition to MOVING
            if (!_destinations.IsEmpty)
            {
                var nextDestination = _destinations.GetNextDestination(CurrentFloor);
                Direction = nextDestination > CurrentFloor ? ValueObjects.Direction.UP : ValueObjects.Direction.DOWN;
                _destinations.SetDirection(Direction);
                State = ElevatorState.MOVING;
                _logger.LogInfo($"Elevator {Id} starting to move {Direction} from floor {CurrentFloor}");
            }
            // Else remain IDLE
        }

        private void ProcessMovingState()
        {
            if (_destinations.IsEmpty)
            {
                // No destinations - transition to IDLE
                Direction = ValueObjects.Direction.IDLE;
                _destinations.SetDirection(Direction);
                State = ElevatorState.IDLE;
                _logger.LogInfo($"Elevator {Id} became IDLE at floor {CurrentFloor}");
                return;
            }

            var nextDestination = _destinations.GetNextDestination(CurrentFloor);

            // Check if arrived at destination
            if (CurrentFloor == nextDestination)
            {
                // Arrived - transition to LOADING
                State = ElevatorState.LOADING;
                _doorTimer = _doorOpenDuration;
                _loadingStateTickCount = 0;
                _logger.LogInfo($"Elevator {Id} arrived at floor {CurrentFloor}, doors opening");
            }
            else
            {
                // Move one floor towards destination
                if (nextDestination > CurrentFloor)
                {
                    CurrentFloor++;
                    _logger.LogDebug($"Elevator {Id} moving UP to floor {CurrentFloor}");
                }
                else
                {
                    CurrentFloor--;
                    _logger.LogDebug($"Elevator {Id} moving DOWN to floor {CurrentFloor}");
                }
            }
        }

        private void ProcessLoadingState()
        {
            _loadingStateTickCount++;

            // Safety timeout check
            if (_loadingStateTickCount > SAFETY_TIMEOUT_TICKS)
            {
                _logger.LogError($"Elevator {Id} stuck in LOADING state for {_loadingStateTickCount} ticks - forcing transition");
                _doorTimer = 0;
                TransitionFromLoading();
                _loadingStateTickCount = 0;
                return;
            }

            // Normal timer decrement
            if (_doorTimer > 0)
            {
                _doorTimer--;
                _logger.LogDebug($"Elevator {Id} door timer: {_doorTimer}");
            }

            if (_doorTimer == 0)
            {
                TransitionFromLoading();
                _loadingStateTickCount = 0;
            }
        }

        private void TransitionFromLoading()
        {
            // Remove current floor from destinations
            _destinations.Remove(CurrentFloor);
            _logger.LogInfo($"Elevator {Id} doors closed at floor {CurrentFloor}");

            // Complete any hall calls for this floor
            CompleteHallCallsAtCurrentFloor();

            // Determine next state
            if (_destinations.IsEmpty)
            {
                // No more destinations - go IDLE
                Direction = ValueObjects.Direction.IDLE;
                _destinations.SetDirection(Direction);
                State = ElevatorState.IDLE;
                _logger.LogInfo($"Elevator {Id} became IDLE at floor {CurrentFloor}");
            }
            else
            {
                // More destinations - continue MOVING
                var nextDestination = _destinations.GetNextDestination(CurrentFloor);
                Direction = nextDestination > CurrentFloor ? ValueObjects.Direction.UP : ValueObjects.Direction.DOWN;
                _destinations.SetDirection(Direction);
                State = ElevatorState.MOVING;
                _logger.LogInfo($"Elevator {Id} continuing {Direction} from floor {CurrentFloor}");
            }
        }

        private void CompleteHallCallsAtCurrentFloor()
        {
            // Note: Hall call completion is handled by Building
            // This method is a placeholder for future logic
        }

        /// <summary>
        /// Gets the current status of this elevator.
        /// </summary>
        public ElevatorStatus GetStatus()
        {
            return new ElevatorStatus(
                Id,
                CurrentFloor,
                Direction,
                State,
                _destinations.GetAll(),
                _assignedHallCallIds.ToList());
        }

        /// <summary>
        /// Gets the list of assigned hall call IDs.
        /// </summary>
        public List<Guid> GetAssignedHallCallIds()
        {
            return _assignedHallCallIds.ToList();
        }

        /// <summary>
        /// Removes a hall call ID from the assigned list (when completed).
        /// </summary>
        public void RemoveHallCallId(Guid hallCallId)
        {
            _assignedHallCallIds.Remove(hallCallId);
        }
    }
}
