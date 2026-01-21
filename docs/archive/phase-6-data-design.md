# Phase 6 - Data Design

## Overview

This phase defines detailed data structures, state machines, field specifications, validation rules, and memory layout.

**Key Principle:** Data structures should enforce invariants and make invalid states unrepresentable.

---

## Entity Data Structures

### 1. Building (Aggregate Root)

```csharp
public class Building
{
    // Configuration (immutable after construction)
    private readonly int _maxFloor;
    private readonly int _elevatorCount;
    private readonly int _doorOpenDurationSeconds;
    
    // State (mutable, protected by lock)
    private readonly List<Elevator> _elevators;
    private readonly HallCallQueue _hallCallQueue;
    private readonly List<Request> _requests;
    
    // Concurrency
    private readonly object _lock = new object();
    
    // Dependencies
    private readonly IScheduler _scheduler;
    private readonly ILogger _logger;
    
    // Constructor
    public Building(
        int maxFloor,
        int elevatorCount,
        int doorOpenDurationSeconds,
        IScheduler scheduler,
        ILogger logger)
    {
        // Validation
        if (maxFloor < 2 || maxFloor > 100)
            throw new ArgumentException("maxFloor must be between 2 and 100");
        if (elevatorCount < 1 || elevatorCount > 10)
            throw new ArgumentException("elevatorCount must be between 1 and 10");
        if (doorOpenDurationSeconds < 1 || doorOpenDurationSeconds > 60)
            throw new ArgumentException("doorOpenDuration must be between 1 and 60");
        
        _maxFloor = maxFloor;
        _elevatorCount = elevatorCount;
        _doorOpenDurationSeconds = doorOpenDurationSeconds;
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Create elevators (entity creation responsibility)
        _elevators = new List<Elevator>(elevatorCount);
        for (int i = 1; i <= elevatorCount; i++)
        {
            _elevators.Add(new Elevator(
                id: i,
                initialFloor: 0,
                maxFloor: maxFloor,
                doorOpenDurationSeconds: doorOpenDurationSeconds,
                logger: logger
            ));
        }
        
        _hallCallQueue = new HallCallQueue(maxFloor, logger);
        _requests = new List<Request>();
    }
    
    // Public API
    public void ProcessRequest(Request request) { /* ... */ }
    public void ProcessTick() { /* ... */ }
    public List<ElevatorStatus> GetAllElevatorStatus() { /* ... */ }
}
```

**Field Specifications:**

| Field | Type | Mutability | Description |
|-------|------|------------|-------------|
| `_maxFloor` | int | Immutable | Max floor number (e.g., 10 for floors 0-9) |
| `_elevatorCount` | int | Immutable | Number of elevators (e.g., 4) |
| `_doorOpenDurationSeconds` | int | Immutable | How long doors stay open (default: 10s) |
| `_elevators` | List<Elevator> | Collection mutable, items mutable | Fixed size after construction |
| `_hallCallQueue` | HallCallQueue | Mutable | Max 18 concurrent hall calls |
| `_requests` | List<Request> | Mutable | Grows unbounded (acceptable for Phase 1) |
| `_lock` | object | Immutable | Global lock for aggregate |
| `_scheduler` | IScheduler | Immutable | Injected dependency |
| `_logger` | ILogger | Immutable | Injected dependency |

**Invariants:**
- `_elevators.Count == _elevatorCount` (never changes after construction)
- All elevator IDs unique (1 to N)
- All elevators have same `_maxFloor`
- `_hallCallQueue.Count <= 18`

**Memory Estimate:**
- Per Building: ~10 KB (mostly elevators and hall calls)

---

### 2. Elevator (Entity)

```csharp
public class Elevator
{
    // Identity
    private readonly int _id;
    
    // Configuration
    private readonly int _maxFloor;
    private readonly int _doorOpenDurationSeconds;
    
    // State
    private int _currentFloor;
    private Direction _direction;
    private ElevatorState _state;
    private DestinationSet _destinations;
    private List<HallCall> _assignedHallCalls;
    private int _doorOpenTicksRemaining;
    
    // Dependencies
    private readonly ILogger _logger;
    
    // Constructor
    public Elevator(
        int id,
        int initialFloor,
        int maxFloor,
        int doorOpenDurationSeconds,
        ILogger logger)
    {
        // Validation
        if (id < 1)
            throw new ArgumentException("id must be >= 1");
        if (initialFloor < 0 || initialFloor >= maxFloor)
            throw new ArgumentException("initialFloor out of range");
        if (maxFloor < 2)
            throw new ArgumentException("maxFloor must be >= 2");
        
        _id = id;
        _maxFloor = maxFloor;
        _doorOpenDurationSeconds = doorOpenDurationSeconds;
        _currentFloor = initialFloor;
        _direction = Direction.IDLE;
        _state = ElevatorState.IDLE;
        _destinations = new DestinationSet(Direction.IDLE, maxFloor);
        _assignedHallCalls = new List<HallCall>();
        _doorOpenTicksRemaining = 0;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    // Public API
    public int Id => _id;
    public int CurrentFloor => _currentFloor;
    public Direction Direction => _direction;
    public ElevatorState State => _state;
    public bool HasPassengers => !_destinations.IsEmpty;
    
    public void AssignHallCall(HallCall hallCall) { /* ... */ }
    public void AddDestination(int floor) { /* ... */ }
    public bool CanAcceptHallCall(HallCall hallCall) { /* ... */ }
    public void ExecuteAction(ElevatorAction action) { /* ... */ }
}
```

**Field Specifications:**

| Field | Type | Mutability | Default | Description |
|-------|------|------------|---------|-------------|
| `_id` | int | Immutable | 1-4 | Elevator identifier |
| `_maxFloor` | int | Immutable | 10 | Copied from Building |
| `_doorOpenDurationSeconds` | int | Immutable | 10 | How long doors stay open |
| `_currentFloor` | int | Mutable | 0 | Current position (0 to maxFloor-1) |
| `_direction` | Direction | Mutable | IDLE | UP, DOWN, or IDLE |
| `_state` | ElevatorState | Mutable | IDLE | IDLE, MOVING, STOPPED, LOADING |
| `_destinations` | DestinationSet | Mutable | Empty | Floors to visit |
| `_assignedHallCalls` | List<HallCall> | Mutable | Empty | Hall calls assigned to this elevator |
| `_doorOpenTicksRemaining` | int | Mutable | 0 | Countdown timer for door open duration |
| `_logger` | ILogger | Immutable | N/A | Injected dependency |

**Invariants:**
- `_currentFloor >= 0 && _currentFloor < _maxFloor`
- `_direction == IDLE` ⟹ `_state == IDLE`
- `_state == MOVING` ⟹ `_destinations.Count > 0`
- `_state == LOADING` ⟹ `_doorOpenTicksRemaining > 0`
- All `_assignedHallCalls` have same direction (if not IDLE)
- All `_assignedHallCalls` are between `_currentFloor` and furthest destination

**Memory Estimate:**
- Per Elevator: ~500 bytes (including destinations and hall calls)

---

### 3. HallCall (Entity)

```csharp
public class HallCall
{
    // Identity
    private readonly Guid _id;
    
    // Data
    private readonly int _floor;
    private readonly Direction _direction;
    private readonly DateTime _createdAt;
    private DestinationSet _destinations;
    
    // Status
    private HallCallStatus _status;
    private int? _assignedElevatorId;
    
    // Dependencies
    private readonly ILogger _logger;
    
    // Constructor
    public HallCall(
        int floor,
        Direction direction,
        int maxFloor,
        DateTime createdAt,
        ILogger logger)
    {
        // Validation
        if (floor < 0 || floor >= maxFloor)
            throw new ArgumentException($"floor must be between 0 and {maxFloor - 1}");
        
        if (direction == Direction.IDLE)
            throw new ArgumentException("HallCall direction cannot be IDLE");
        
        if (floor == 0 && direction == Direction.DOWN)
            throw new ArgumentException("Floor 0 cannot have DOWN hall call");
        
        if (floor == maxFloor - 1 && direction == Direction.UP)
            throw new ArgumentException($"Floor {maxFloor - 1} cannot have UP hall call");
        
        _id = Guid.NewGuid();
        _floor = floor;
        _direction = direction;
        _createdAt = createdAt;
        _destinations = new DestinationSet(direction, maxFloor);
        _status = HallCallStatus.PENDING;
        _assignedElevatorId = null;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    // Public API
    public Guid Id => _id;
    public int Floor => _floor;
    public Direction Direction => _direction;
    public DateTime CreatedAt => _createdAt;
    public HallCallStatus Status => _status;
    public int? AssignedElevatorId => _assignedElevatorId;
    
    public void AddDestination(int destination) { /* ... */ }
    public void MarkAsAssigned(int elevatorId) { /* ... */ }
    public void MarkAsCompleted() { /* ... */ }
}
```

**Field Specifications:**

| Field | Type | Mutability | Description |
|-------|------|------------|-------------|
| `_id` | Guid | Immutable | Unique identifier |
| `_floor` | int | Immutable | Floor where hall button pressed |
| `_direction` | Direction | Immutable | UP or DOWN (never IDLE) |
| `_createdAt` | DateTime | Immutable | Timestamp when created |
| `_destinations` | DestinationSet | Mutable | Passenger destinations |
| `_status` | HallCallStatus | Mutable | PENDING, ASSIGNED, COMPLETED |
| `_assignedElevatorId` | int? | Mutable | Nullable, set when assigned |
| `_logger` | ILogger | Immutable | Injected dependency |

**Invariants:**
- `_direction != Direction.IDLE`
- `_floor == 0` ⟹ `_direction == UP`
- `_floor == maxFloor - 1` ⟹ `_direction == DOWN`
- `_destinations.Direction == _direction`
- `_destinations.IsEmpty == false` (always has at least one destination)
- `_status == ASSIGNED` ⟹ `_assignedElevatorId != null`

**Memory Estimate:**
- Per HallCall: ~200 bytes

---

### 4. Request (Entity)

```csharp
public class Request
{
    // Identity
    private readonly Guid _id;
    
    // Data
    private readonly Journey _journey;
    private readonly DateTime _createdAt;
    
    // Status
    private RequestStatus _status;
    private Guid? _assignedHallCallId;
    private DateTime? _completedAt;
    
    // Constructor
    public Request(int source, int destination, int maxFloor, DateTime createdAt)
    {
        // Validation
        if (source < 0 || source >= maxFloor)
            throw new ArgumentException($"source must be between 0 and {maxFloor - 1}");
        
        if (destination < 0 || destination >= maxFloor)
            throw new ArgumentException($"destination must be between 0 and {maxFloor - 1}");
        
        if (source == destination)
            throw new ArgumentException("source and destination cannot be the same");
        
        _id = Guid.NewGuid();
        _journey = new Journey(source, destination);
        _createdAt = createdAt;
        _status = RequestStatus.CREATED;
        _assignedHallCallId = null;
        _completedAt = null;
    }
    
    // Public API
    public Guid Id => _id;
    public Journey Journey => _journey;
    public DateTime CreatedAt => _createdAt;
    public RequestStatus Status => _status;
    public Guid? AssignedHallCallId => _assignedHallCallId;
    public DateTime? CompletedAt => _completedAt;
    
    public TimeSpan? WaitTime => 
        _completedAt.HasValue ? _completedAt.Value - _createdAt : null;
    
    public void MarkAsAssignedToHallCall(Guid hallCallId) { /* ... */ }
    public void MarkAsCompleted(DateTime completedAt) { /* ... */ }
}
```

**Field Specifications:**

| Field | Type | Mutability | Description |
|-------|------|------------|-------------|
| `_id` | Guid | Immutable | Unique identifier |
| `_journey` | Journey | Immutable | Source, destination, direction |
| `_createdAt` | DateTime | Immutable | Request timestamp |
| `_status` | RequestStatus | Mutable | CREATED, ASSIGNED_TO_HALLCALL, COMPLETED |
| `_assignedHallCallId` | Guid? | Mutable | Nullable, set when assigned |
| `_completedAt` | DateTime? | Mutable | Nullable, set when completed |

**Invariants:**
- `_journey.Source != _journey.Destination`
- `_status == ASSIGNED_TO_HALLCALL` ⟹ `_assignedHallCallId != null`
- `_status == COMPLETED` ⟹ `_completedAt != null`
- `_completedAt` ⟹ `_completedAt > _createdAt`

**Memory Estimate:**
- Per Request: ~100 bytes

---

## Value Object Data Structures

### 1. Journey (Value Object - Record)

```csharp
public record Journey
{
    public int Source { get; init; }
    public int Destination { get; init; }
    public Direction Direction { get; init; }
    
    public Journey(int source, int destination)
    {
        if (source == destination)
            throw new ArgumentException("Source and destination cannot be the same");
        
        Source = source;
        Destination = destination;
        Direction = destination > source ? Direction.UP : Direction.DOWN;
    }
    
    public int Distance => Math.Abs(Destination - Source);
}
```

**Field Specifications:**

| Field | Type | Description |
|-------|------|-------------|
| `Source` | int | Starting floor |
| `Destination` | int | Target floor |
| `Direction` | Direction | Calculated: UP if dest > source, DOWN otherwise |

**Invariants:**
- `Source != Destination`
- `Direction == UP` ⟺ `Destination > Source`
- `Direction == DOWN` ⟺ `Destination < Source`

**Immutability:** C# record (value equality, immutable by default)

**Memory:** ~12 bytes

---

### 2. DestinationSet (Value Object)

```csharp
public class DestinationSet
{
    private readonly SortedSet<int> _destinations;
    private readonly Direction _direction;
    private readonly int _maxFloor;
    
    public DestinationSet(Direction direction, int maxFloor)
    {
        _destinations = new SortedSet<int>();
        _direction = direction;
        _maxFloor = maxFloor;
    }
    
    public bool IsEmpty => _destinations.Count == 0;
    public int Count => _destinations.Count;
    public Direction Direction => _direction;
    
    public void Add(int floor)
    {
        // Validation
        if (floor < 0 || floor >= _maxFloor)
            throw new ArgumentException($"floor must be between 0 and {_maxFloor - 1}");
        
        // Direction compatibility check
        if (_direction == Direction.UP && floor < _destinations.Min())
            throw new ArgumentException("Cannot add floor below current minimum for UP direction");
        
        if (_direction == Direction.DOWN && floor > _destinations.Max())
            throw new ArgumentException("Cannot add floor above current maximum for DOWN direction");
        
        _destinations.Add(floor); // SortedSet handles duplicates
    }
    
    public void Remove(int floor)
    {
        _destinations.Remove(floor);
    }
    
    public bool Contains(int floor)
    {
        return _destinations.Contains(floor);
    }
    
    public int GetNextDestination(int currentFloor)
    {
        if (_destinations.Count == 0)
            throw new InvalidOperationException("No destinations");
        
        if (_direction == Direction.UP)
        {
            // Return smallest destination >= currentFloor
            var candidates = _destinations.Where(d => d >= currentFloor).ToList();
            if (candidates.Any())
                return candidates.Min(); // Next floor going up
            else
                return _destinations.Max(); // Wrap around - furthest floor
        }
        else if (_direction == Direction.DOWN)
        {
            // Return largest destination <= currentFloor
            var candidates = _destinations.Where(d => d <= currentFloor).ToList();
            if (candidates.Any())
                return candidates.Max(); // Next floor going down
            else
                return _destinations.Min(); // Wrap around - lowest floor (including floor 0!)
        }
        else // IDLE
        {
            // Return nearest
            return _destinations.OrderBy(d => Math.Abs(d - currentFloor)).First();
        }
    }
    
    public int GetFurthestDestination()
    {
        if (_destinations.Count == 0)
            throw new InvalidOperationException("No destinations");
        
        return _direction == Direction.UP ? _destinations.Max() : _destinations.Min();
    }
    
    public List<int> GetAll() => _destinations.ToList();
}
```

**Field Specifications:**

| Field | Type | Description |
|-------|------|-------------|
| `_destinations` | SortedSet<int> | Floors to visit (sorted ascending) |
| `_direction` | Direction | UP, DOWN, or IDLE |
| `_maxFloor` | int | Validation limit |

**Invariants:**
- All destinations in range [0, maxFloor)
- No duplicates (SortedSet guarantees)
- Direction compatibility validated on Add()

**Key Methods:**
- `GetNextDestination(currentFloor)`: Returns next floor to visit based on direction
- `GetFurthestDestination()`: Returns max (UP) or min (DOWN)

**Memory:** ~40 bytes + (8 bytes × number of destinations)

---

### 3. HallCallQueue (Value Object)

```csharp
public class HallCallQueue
{
    private readonly Dictionary<(int Floor, Direction Direction), HallCall> _hallCalls;
    private readonly int _maxFloor;
    private readonly ILogger _logger;
    private const int MAX_HALL_CALLS = 18;
    
    public HallCallQueue(int maxFloor, ILogger logger)
    {
        _hallCalls = new Dictionary<(int, Direction), HallCall>();
        _maxFloor = maxFloor;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public int Count => _hallCalls.Count;
    public bool IsFull => _hallCalls.Count >= MAX_HALL_CALLS;
    
    public HallCall? GetOrCreate(int floor, Direction direction, DateTime timestamp)
    {
        // Check capacity
        if (!_hallCalls.ContainsKey((floor, direction)) && IsFull)
        {
            _logger.LogWarning($"HallCallQueue full ({MAX_HALL_CALLS}/{MAX_HALL_CALLS})");
            return null;
        }
        
        // Get existing or create new
        var key = (floor, direction);
        if (!_hallCalls.TryGetValue(key, out var hallCall))
        {
            hallCall = new HallCall(floor, direction, _maxFloor, timestamp, _logger);
            _hallCalls[key] = hallCall;
            _logger.LogInfo($"HallCall created: Floor={floor}, Direction={direction}, Id={hallCall.Id}");
        }
        
        return hallCall;
    }
    
    public void Remove(HallCall hallCall)
    {
        var key = (hallCall.Floor, hallCall.Direction);
        _hallCalls.Remove(key);
    }
    
    public List<HallCall> GetPending()
    {
        return _hallCalls.Values
            .Where(hc => hc.Status == HallCallStatus.PENDING)
            .ToList();
    }
    
    public List<HallCall> GetAll()
    {
        return _hallCalls.Values.ToList();
    }
}
```

**Field Specifications:**

| Field | Type | Description |
|-------|------|-------------|
| `_hallCalls` | Dictionary<(int, Direction), HallCall> | Keyed by floor and direction |
| `_maxFloor` | int | Validation limit |
| `_logger` | ILogger | For logging |
| `MAX_HALL_CALLS` | const int | 18 (2 per floor × 9 floors max) |

**Invariants:**
- `Count <= MAX_HALL_CALLS`
- No duplicate keys (Dictionary guarantees)
- Each key represents unique (floor, direction) combination

**Key Operations:**
- `GetOrCreate()`: Idempotent hall call creation
- `GetPending()`: Filter for assignment
- `Remove()`: Called when hall call completed

**Memory:** ~500 bytes + (200 bytes × number of hall calls)

---

### 4. ElevatorStatus (Value Object - Record)

```csharp
public record ElevatorStatus
{
    public int Id { get; init; }
    public int CurrentFloor { get; init; }
    public Direction Direction { get; init; }
    public ElevatorState State { get; init; }
    public List<int> Destinations { get; init; }
    public List<Guid> AssignedHallCallIds { get; init; }
    public bool HasPassengers { get; init; }
    public DateTime Timestamp { get; init; }
    
    public ElevatorStatus(
        int id,
        int currentFloor,
        Direction direction,
        ElevatorState state,
        List<int> destinations,
        List<Guid> assignedHallCallIds,
        bool hasPassengers,
        DateTime timestamp)
    {
        Id = id;
        CurrentFloor = currentFloor;
        Direction = direction;
        State = state;
        Destinations = destinations.ToList(); // Defensive copy
        AssignedHallCallIds = assignedHallCallIds.ToList(); // Defensive copy
        HasPassengers = hasPassengers;
        Timestamp = timestamp;
    }
}
```

**Field Specifications:**

| Field | Type | Description |
|-------|------|-------------|
| `Id` | int | Elevator identifier |
| `CurrentFloor` | int | Snapshot of position |
| `Direction` | Direction | Snapshot of direction |
| `State` | ElevatorState | Snapshot of state |
| `Destinations` | List<int> | Copy of destination floors |
| `AssignedHallCallIds` | List<Guid> | Copy of hall call IDs |
| `HasPassengers` | bool | Whether has any destinations |
| `Timestamp` | DateTime | When snapshot taken |

**Purpose:** Immutable snapshot for queries and logging.

**Memory:** ~100 bytes + (8 bytes × destinations) + (16 bytes × hall calls)

---

## Enum Definitions

### Direction

```csharp
public enum Direction
{
    IDLE = 0,
    UP = 1,
    DOWN = -1
}
```

**Values:**
- `IDLE`: No direction (elevator stationary)
- `UP`: Moving upward
- `DOWN`: Moving downward

**Usage:** Elevator direction, hall call direction, journey direction

---

### ElevatorState

```csharp
public enum ElevatorState
{
    IDLE,      // Not moving, no destinations
    MOVING,    // In motion between floors
    STOPPED,   // Arrived at floor, doors closed
    LOADING    // Doors open, passengers boarding/alighting
}
```

**State Transitions:** See state machine diagram below.

---

### HallCallStatus

```csharp
public enum HallCallStatus
{
    PENDING,    // Created, waiting for elevator assignment
    ASSIGNED,   // Assigned to an elevator
    COMPLETED   // Passengers picked up, hall call fulfilled
}
```

---

### RequestStatus

```csharp
public enum RequestStatus
{
    CREATED,              // Request received
    ASSIGNED_TO_HALLCALL, // Assigned to a hall call
    COMPLETED             // Passenger journey complete
}
```

---

### ElevatorAction

```csharp
public enum ElevatorAction
{
    IDLE,         // Do nothing
    MOVE_UP,      // Move up one floor
    MOVE_DOWN,    // Move down one floor
    OPEN_DOORS,   // Open doors (transition to LOADING)
    CLOSE_DOORS   // Close doors (transition to MOVING or IDLE)
}
```

**Usage:** Output of `IElevatorMovementCoordinator.DecideNextAction()`

---

## State Machines

### Elevator State Machine

```
┌─────────────────────────────────────────────────────────────┐
│                     IDLE                                    │
│  - No destinations                                          │
│  - Direction = IDLE                                         │
│  - Doors closed                                             │
└─────────────────────────────────────────────────────────────┘
         │                                    ▲
         │ Destination added                   │ No more destinations
         │ Action: MOVE_UP/MOVE_DOWN           │ Action: CLOSE_DOORS
         ▼                                    │
┌─────────────────────────────────────────────────────────────┐
│                    MOVING                                   │
│  - Has destinations                                         │
│  - Direction = UP or DOWN                                   │
│  - Moving between floors                                    │
└─────────────────────────────────────────────────────────────┘
         │                                    ▲
         │ Reached destination floor           │ More destinations
         │ Action: (internal transition)       │ Action: CLOSE_DOORS
         ▼                                    │
┌─────────────────────────────────────────────────────────────┐
│                   STOPPED                                   │
│  - At destination floor                                     │
│  - Direction = UP or DOWN (maintained)                      │
│  - Doors closed                                             │
└─────────────────────────────────────────────────────────────┘
         │                                    ▲
         │ Action: OPEN_DOORS                  │
         ▼                                    │
┌─────────────────────────────────────────────────────────────┐
│                   LOADING                                   │
│  - Doors open                                               │
│  - Timer: _doorOpenTicksRemaining (default: 10 ticks)       │
│  - Can add new destinations while loading                   │
└─────────────────────────────────────────────────────────────┘
         │
         │ Timer expires
         │ Action: CLOSE_DOORS
         ▼
    Back to MOVING or IDLE
```

**Special Case: Destinations Added While STOPPED**

If elevator is in STOPPED state and about to execute OPEN_DOORS:
- New destinations added DO NOT interrupt door sequence
- Doors still open (STOPPED → LOADING)
- After doors close, elevator will move to new destinations

**Example:**
```
Tick N:   Elevator at floor 5, STOPPED (arrived at destination)
Tick N+1: New hall call assigned, adds destination 8
          But elevator still executes OPEN_DOORS (STOPPED → LOADING)
Tick N+2 to N+11: Doors open (LOADING, timer counts down)
Tick N+12: Doors close (LOADING → MOVING toward floor 8)
```

This ensures door sequence is not interrupted by new assignments.

---

**Key Transitions:**

| From | To | Trigger | Action | Side Effects |
|------|-----|---------|--------|--------------|
| IDLE | MOVING | Destination added | MOVE_UP or MOVE_DOWN | - |
| MOVING | STOPPED | Reached destination | (automatic) | - |
| STOPPED | LOADING | Open doors | OPEN_DOORS | **Remove currentFloor from destinations** |
| LOADING | MOVING | Doors close, has destinations | CLOSE_DOORS | **Complete hall calls at this floor** |
| LOADING | IDLE | Doors close, no destinations | CLOSE_DOORS | **Complete hall calls at this floor** |

**Door Timer Logic:**
```csharp
// When entering LOADING state (STOPPED → LOADING via OPEN_DOORS)
_doorOpenTicksRemaining = _doorOpenDurationSeconds;

// Each tick in ProcessTick() - BEFORE DecideNextAction()
if (elevator.State == LOADING)
{
    elevator.DecrementDoorTimer();
    
    if (elevator.DoorTimerExpired)
    {
        // Timer reached 0, doors should close
        // Complete hall calls DURING door closing
        CompleteHallCallsAtFloor(elevator);
        elevator.ExecuteAction(CLOSE_DOORS);
    }
}

// Elevator method
public void DecrementDoorTimer()
{
    if (_state == LOADING && _doorOpenTicksRemaining > 0)
    {
        _doorOpenTicksRemaining--;
    }
}

public bool DoorTimerExpired => 
    _state == LOADING && _doorOpenTicksRemaining == 0;
```

**Fixed Duration:** Timer does NOT reset if new destinations added while LOADING.

**Timing Clarification:**
- Door timer decrements in `ProcessTick()` BEFORE calling `DecideNextAction()`
- This ensures timer countdown happens every tick for LOADING elevators
- When timer reaches 0, CLOSE_DOORS action is executed immediately

---

### HallCall Lifecycle

```
┌─────────────────┐
│    PENDING      │ ← Created, waiting for elevator
└─────────────────┘
         │
         │ IScheduler.SelectElevator() finds match
         │ Elevator.AssignHallCall()
         ▼
┌─────────────────┐
│    ASSIGNED     │ ← Assigned to specific elevator
└─────────────────┘
         │
         │ Elevator reaches floor in correct direction
         │ Elevator leaves floor
         ▼
┌─────────────────┐
│   COMPLETED     │ ← Passengers picked up
└─────────────────┘
```

**Completion Criteria:**
- Elevator at `hallCall.Floor`
- Elevator direction matches `hallCall.Direction`
- Elevator in LOADING state (doors have been open)
- Door timer expired (`_doorOpenTicksRemaining == 0`)
- Hall call marked COMPLETED during CLOSE_DOORS action execution

**Timing Details:**
```
Tick N:   Elevator arrives at floor (MOVING → STOPPED)
Tick N+1: Doors open (STOPPED → LOADING, timer = 10)
          Destination removed from queue at this point
Tick N+2 to N+10: Doors stay open (LOADING, timer counts down)
Tick N+11: Timer = 0, ProcessTick() detects timer expired
           CompleteHallCallsAtFloor() called
           ↓ Hall call marked COMPLETED here
           ExecuteAction(CLOSE_DOORS)
           State: LOADING → MOVING/IDLE
```

**Important:** Hall call completes when doors START closing (CLOSE_DOORS action), not when elevator leaves the floor.

---

### Request Lifecycle

```
┌─────────────────┐
│     CREATED     │ ← Request received
└─────────────────┘
         │
         │ Building.ProcessRequest()
         │ Assigned to HallCall (new or existing)
         ▼
┌─────────────────────────┐
│ ASSIGNED_TO_HALLCALL    │ ← Linked to hall call
└─────────────────────────┘
         │
         │ Hall call completed
         │ Elevator leaves source floor
         ▼
┌─────────────────┐
│   COMPLETED     │ ← Journey fulfilled
└─────────────────┘
```

**Note:** We don't track "in elevator" separately. COMPLETED means picked up.

---

## Data Validation Rules

### Floor Validation

| Context | Rule |
|---------|------|
| **All** | `0 <= floor < maxFloor` |
| **HallCall UP** | `floor < maxFloor - 1` (can't go up from top floor) |
| **HallCall DOWN** | `floor > 0` (can't go down from ground floor) |
| **Request source** | `0 <= source < maxFloor` |
| **Request destination** | `0 <= destination < maxFloor` |
| **Request** | `source != destination` |

### Direction Validation

| Context | Rule |
|---------|------|
| **HallCall** | `direction == UP or DOWN` (never IDLE) |
| **Elevator** | `direction == IDLE` ⟹ `state == IDLE` |
| **DestinationSet** | All destinations compatible with direction |

### Capacity Validation

| Entity | Limit | Validation |
|--------|-------|------------|
| **HallCallQueue** | 18 max | Check before creating new hall call |
| **Elevator destinations** | No limit | (Implicitly limited by building floors) |
| **Elevator hall calls** | No limit | (Self-regulating via direction/range rules) |

### Time Validation

| Field | Rule |
|-------|------|
| **Request.CreatedAt** | Cannot be in future |
| **Request.CompletedAt** | Must be > CreatedAt |
| **HallCall.CreatedAt** | Cannot be in future |

---

## Memory Layout

### Building Aggregate

```
Building (10 KB)
├── Configuration (24 bytes)
│   ├── _maxFloor: 4 bytes
│   ├── _elevatorCount: 4 bytes
│   └── _doorOpenDurationSeconds: 4 bytes
│
├── Elevators (4 × 500 bytes = 2 KB)
│   ├── Elevator 1
│   ├── Elevator 2
│   ├── Elevator 3
│   └── Elevator 4
│
├── HallCallQueue (500 bytes + 18 × 200 bytes = 4 KB)
│   └── Max 18 HallCall entities
│
└── Requests (N × 100 bytes)
    └── Grows unbounded (260+ days capacity at 500MB limit)
```

### Per-Request Memory Growth

```
Request generation: 1 request / 5 seconds
Requests per hour: 720
Memory per hour: 72 KB
Memory per day: 1.7 MB
Memory per month: 51 MB
Memory per year: 614 MB

At 500 MB limit:
Maximum runtime: ~260 days (8+ months)
```

**Conclusion:** Memory not a concern for Phase 1 simulations.

---

## Data Access Patterns

### Write Operations (Require Lock)

| Operation | Lock Scope | Components Modified |
|-----------|------------|---------------------|
| **ProcessRequest** | Entire method | Building, HallCallQueue, Request |
| **ProcessTick** | Entire method | Building, all Elevators, HallCallQueue |
| **AssignHallCall** | Within ProcessRequest | Elevator, HallCall |
| **CompleteHallCall** | Within ProcessTick | Elevator, HallCall, Requests |

### Read Operations (Require Lock for Consistency)

| Operation | Lock Scope | Components Read |
|-----------|------------|-----------------|
| **GetAllElevatorStatus** | Entire method | All Elevators |
| **IScheduler.SelectElevator** | Within ProcessRequest | All Elevators (read-only) |

### Traversal Patterns

**ProcessTick - Sequential Elevator Processing:**
```csharp
lock (_lock)
{
    // Retry pending hall calls
    foreach (var hallCall in _hallCallQueue.GetPending())
    {
        // ... try to assign
    }
    
    // Process elevators in fixed order: 1, 2, 3, 4
    foreach (var elevator in _elevators)
    {
        // Decide action
        // Execute action
        // Complete hall calls if applicable
    }
}
```

**Hall Call Completion - Check All Assigned:**
```csharp
// For elevator at floor X, direction UP
foreach (var hallCall in elevator.AssignedHallCalls.ToList())
{
    if (hallCall.Floor == elevator.CurrentFloor &&
        hallCall.Direction == elevator.Direction)
    {
        hallCall.MarkAsCompleted();
        elevator.RemoveHallCall(hallCall);
        _hallCallQueue.Remove(hallCall);
        
        // Mark all requests in this hall call as completed
        foreach (var request in _requests.Where(r => r.AssignedHallCallId == hallCall.Id))
        {
            request.MarkAsCompleted(DateTime.UtcNow);
        }
    }
}
```

---

## Configuration Data Structure

### SimulationConfiguration

```csharp
public class SimulationConfiguration
{
    // Building
    public int FloorCount { get; init; }
    public int ElevatorCount { get; init; }
    public int DoorOpenDurationSeconds { get; init; }
    
    // Timing
    public int SimulationTickMs { get; init; }
    public int RequestFrequencyMs { get; init; }
    
    // Request Generation
    public int MinFloor { get; init; }
    public int MaxFloor { get; init; }
    
    // Validation
    public void Validate()
    {
        if (FloorCount < 2 || FloorCount > 100)
            throw new ArgumentException("FloorCount must be between 2 and 100");
        
        if (ElevatorCount < 1 || ElevatorCount > 10)
            throw new ArgumentException("ElevatorCount must be between 1 and 10");
        
        if (DoorOpenDurationSeconds < 1 || DoorOpenDurationSeconds > 60)
            throw new ArgumentException("DoorOpenDurationSeconds must be between 1 and 60");
        
        if (SimulationTickMs < 100 || SimulationTickMs > 10000)
            throw new ArgumentException("SimulationTickMs must be between 100 and 10000");
        
        if (RequestFrequencyMs < 1000 || RequestFrequencyMs > 60000)
            throw new ArgumentException("RequestFrequencyMs must be between 1000 and 60000");
        
        if (MinFloor < 0 || MinFloor >= FloorCount)
            throw new ArgumentException("MinFloor out of range");
        
        if (MaxFloor < 0 || MaxFloor >= FloorCount)
            throw new ArgumentException("MaxFloor out of range");
        
        if (MinFloor >= MaxFloor)
            throw new ArgumentException("MinFloor must be < MaxFloor");
    }
    
    // Defaults
    public static SimulationConfiguration Default => new()
    {
        FloorCount = 10,
        ElevatorCount = 4,
        DoorOpenDurationSeconds = 10,
        SimulationTickMs = 1000,
        RequestFrequencyMs = 5000,
        MinFloor = 0,
        MaxFloor = 9
    };
    
    // Load from JSON
    public static SimulationConfiguration Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Config file not found: {filePath}. Using defaults.");
            return Default;
        }
        
        try
        {
            var json = File.ReadAllText(filePath);
            var config = JsonSerializer.Deserialize<SimulationConfiguration>(json);
            config.Validate();
            return config;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load configuration: {ex.Message}", ex);
        }
    }
}
```

### JSON Format

```json
{
  "floorCount": 10,
  "elevatorCount": 4,
  "doorOpenDurationSeconds": 10,
  "simulationTickMs": 1000,
  "requestFrequencyMs": 5000,
  "minFloor": 0,
  "maxFloor": 9
}
```

---

## Summary

### Key Data Design Decisions

| Aspect | Decision | Rationale |
|--------|----------|-----------|
| **Door duration** | 10s (configurable) | Realistic, testable with fast time |
| **Door timer** | Fixed (no reset) | Simpler, predictable |
| **Destination storage** | SortedSet | Efficient, handles duplicates, ordered |
| **Direction handling** | Store ascending, query by direction | Simple, works for UP/DOWN |
| **HallCall destinations** | DestinationSet (value object) | Encapsulation, validation |
| **Request statuses** | 3 statuses (simple) | Sufficient for Phase 1 |
| **Elevator collection** | List<Elevator> | Fixed order, only 4 items |
| **Request storage** | Keep all (no cleanup) | 260+ days capacity, metrics needed |
| **HallCallQueue key** | Tuple<int, Direction> | Simple, built-in equality |
| **Elevator hall calls** | List<HallCall> | Direct access, no lookups |
| **Value objects** | C# records (immutable) | Thread-safe, value equality |
| **IDs** | GUID (Request, HallCall), int (Elevator) | Uniqueness vs readability |
| **Validation** | Constructor validation | One creation path, fail-fast |
| **Memory** | No limits for Phase 1 | 260+ days capacity at 500MB |
| **Null handling** | Nullable reference types | Modern C#, clear intent |

### Data Structures Summary

| Entity/Value Object | Size | Mutability | Key Fields |
|---------------------|------|------------|------------|
| **Building** | 10 KB | Aggregate mutable | Elevators, HallCallQueue, Requests |
| **Elevator** | 500 bytes | Mutable | CurrentFloor, State, Destinations, HallCalls |
| **HallCall** | 200 bytes | Mutable | Floor, Direction, Destinations, Status |
| **Request** | 100 bytes | Mutable | Journey, Status, Timestamps |
| **Journey** | 12 bytes | Immutable | Source, Destination, Direction |
| **DestinationSet** | 40+ bytes | Mutable collection | SortedSet<int> |
| **HallCallQueue** | 4 KB | Mutable collection | Dictionary<(int, Direction), HallCall> |
| **ElevatorStatus** | 100+ bytes | Immutable | Snapshot of elevator state |

### State Machines Defined

1. ✅ Elevator: IDLE ⟷ MOVING ⟷ STOPPED ⟷ LOADING
2. ✅ HallCall: PENDING → ASSIGNED → COMPLETED
3. ✅ Request: CREATED → ASSIGNED_TO_HALLCALL → COMPLETED

### Next Steps

With data design complete, proceed to:
- **Phase 7**: APIs & Contracts (interface signatures, method contracts)
- **Phase 8**: Failure Modes (error scenarios, mitigation)
- **Phase 9**: Scalability (bottlenecks, scaling strategies)
