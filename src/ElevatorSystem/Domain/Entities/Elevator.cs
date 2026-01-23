using System;
using System.Collections.Generic;
using System.Linq;
using ElevatorSystem.Domain.ValueObjects;
using ElevatorSystem.Infrastructure.Logging;

namespace ElevatorSystem.Domain.Entities
{
    /// <summary>
    /// Represents an elevator car with its state and behavior.
    /// NOT thread-safe: Must be accessed only through Building's lock.
    /// </summary>
    public class Elevator
    {
        private const int SAFETY_TIMEOUT_TICKS = 10;
        
        public int Id { get; }
        public int CurrentFloor { get; private set; }
        public Direction Direction { get; private set; }
        public ElevatorState State { get; private set; }

        private readonly ILogger _logger;
        private readonly int _maxFloors;
        private readonly int _doorOpenDuration;
        private readonly int _movementTicks;
        private readonly DestinationSet _destinations;
        private readonly List<Guid> _assignedHallCallIds;
        private int _doorTimer;
        private int _loadingStateTickCount;
        private int _movementTimer;

        public Elevator(int id, int maxFloors, int doorOpenDuration, int movementTicks, ILogger logger)
        {
            Id = id;
            _maxFloors = maxFloors;
            _doorOpenDuration = doorOpenDuration;
            _movementTicks = movementTicks;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            CurrentFloor = 0;
            Direction = ValueObjects.Direction.IDLE;
            State = ElevatorState.IDLE;
            _destinations = new DestinationSet(ValueObjects.Direction.IDLE);
            _assignedHallCallIds = new List<Guid>();
            _doorTimer = 0;
            _loadingStateTickCount = 0;
            _movementTimer = 0;
        }

        /// <summary>
        /// Checks if this elevator can accept a hall call.
        /// </summary>
        public bool CanAcceptHallCall(HallCall hallCall)
        {
            if (IsAlreadyServicingFloor(hallCall))
            {
                return false;
            }

            if (State == ElevatorState.IDLE)
            {
                return true;
            }

            if (Direction != hallCall.Direction)
            {
                return false;
            }

            return IsHallCallOnRoute(hallCall);
        }

        private bool IsAlreadyServicingFloor(HallCall hallCall)
        {
            return CurrentFloor == hallCall.Floor &&
                   Direction == hallCall.Direction &&
                   State == ElevatorState.LOADING;
        }

        private bool IsHallCallOnRoute(HallCall hallCall)
        {
            if (_destinations.IsEmpty)
            {
                return false;
            }

            var furthestDestination = _destinations.GetFurthestDestination();

            if (Direction == ValueObjects.Direction.UP)
            {
                return CurrentFloor < hallCall.Floor && hallCall.Floor <= furthestDestination;
            }
            else
            {
                return CurrentFloor > hallCall.Floor && hallCall.Floor >= furthestDestination;
            }
        }

        /// <summary>
        /// Assigns a hall call to this elevator.
        /// </summary>
        public void AssignHallCall(HallCall hallCall)
        {
            _assignedHallCallIds.Add(hallCall.Id);
            
            if (CurrentFloor != hallCall.Floor)
            {
                _destinations.Add(hallCall.Floor);
            }
            
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
            if (_destinations.IsEmpty)
            {
                return;
            }

            var nextDestination = _destinations.GetNextDestination(CurrentFloor);
            
            if (nextDestination == CurrentFloor)
            {
                HandleAlreadyAtDestination(nextDestination);
                return;
            }

            Direction = nextDestination > CurrentFloor ? ValueObjects.Direction.UP : ValueObjects.Direction.DOWN;
            _destinations.SetDirection(Direction);
            State = ElevatorState.MOVING;
            _movementTimer = _movementTicks; // Initialize movement timer
            _logger.LogInfo($"Elevator {Id} starting to move {Direction} from floor {CurrentFloor}");
        }

        private void HandleAlreadyAtDestination(int destination)
        {
            _destinations.Remove(destination);
            if (!_destinations.IsEmpty)
            {
                ProcessIdleState();
            }
        }

        private void ProcessMovingState()
        {
            if (_destinations.IsEmpty)
            {
                TransitionToIdle();
                return;
            }

            var nextDestination = _destinations.GetNextDestination(CurrentFloor);

            if (CurrentFloor == nextDestination)
            {
                TransitionToLoading();
            }
            else
            {
                // Wait for movement timer before moving one floor
                _movementTimer--;
                
                if (_movementTimer <= 0)
                {
                    MoveOneFloorTowards(nextDestination);
                    _movementTimer = _movementTicks; // Reset timer for next movement
                }
                else
                {
                    _logger.LogDebug($"Elevator {Id} movement timer: {_movementTimer}");
                }
            }
        }

        private void TransitionToIdle()
        {
            Direction = ValueObjects.Direction.IDLE;
            _destinations.SetDirection(Direction);
            State = ElevatorState.IDLE;
            _logger.LogInfo($"Elevator {Id} became IDLE at floor {CurrentFloor}");
        }

        private void TransitionToLoading()
        {
            State = ElevatorState.LOADING;
            _doorTimer = _doorOpenDuration;
            _loadingStateTickCount = 0;
            _logger.LogInfo($"Elevator {Id} arrived at floor {CurrentFloor}, doors opening");
        }

        private void MoveOneFloorTowards(int destination)
        {
            if (destination > CurrentFloor)
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

        private void ProcessLoadingState()
        {
            _loadingStateTickCount++;

            if (IsSafetyTimeoutExceeded())
            {
                HandleSafetyTimeout();
                return;
            }

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

        private bool IsSafetyTimeoutExceeded()
        {
            return _loadingStateTickCount > SAFETY_TIMEOUT_TICKS;
        }

        private void HandleSafetyTimeout()
        {
            _logger.LogError($"Elevator {Id} stuck in LOADING state for {_loadingStateTickCount} ticks - forcing transition");
            _doorTimer = 0;
            TransitionFromLoading();
            _loadingStateTickCount = 0;
        }

        private void TransitionFromLoading()
        {
            _destinations.Remove(CurrentFloor);
            _logger.LogInfo($"Elevator {Id} doors closed at floor {CurrentFloor}");

            if (_destinations.IsEmpty)
            {
                TransitionToIdleAfterLoading();
            }
            else
            {
                ContinueMovingToNextDestination();
            }
        }

        private void TransitionToIdleAfterLoading()
        {
            Direction = ValueObjects.Direction.IDLE;
            _destinations.SetDirection(Direction);
            State = ElevatorState.IDLE;
            _logger.LogInfo($"Elevator {Id} became IDLE at floor {CurrentFloor}");
        }

        private void ContinueMovingToNextDestination()
        {
            var nextDestination = _destinations.GetNextDestination(CurrentFloor);
            
            if (nextDestination == CurrentFloor)
            {
                HandleInvalidNextDestination(nextDestination);
                return;
            }

            Direction = nextDestination > CurrentFloor ? ValueObjects.Direction.UP : ValueObjects.Direction.DOWN;
            _destinations.SetDirection(Direction);
            State = ElevatorState.MOVING;
            _movementTimer = _movementTicks; // Initialize movement timer
            _logger.LogInfo($"Elevator {Id} continuing {Direction} from floor {CurrentFloor}");
        }

        private void HandleInvalidNextDestination(int nextDestination)
        {
            _logger.LogError($"Elevator {Id} next destination equals current floor {CurrentFloor} - removing and going IDLE");
            _destinations.Remove(nextDestination);
            Direction = ValueObjects.Direction.IDLE;
            _destinations.SetDirection(Direction);
            State = ElevatorState.IDLE;
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
