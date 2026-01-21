# Phase 3 - Domain Model

## Core Concepts Overview

The domain model identifies the key concepts, their relationships, and invariants without diving into implementation details.

### Concept Count
- **4 Entities:** Building, Elevator, HallCall, Request
- **8 Value Objects:** Direction, ElevatorState, HallCallStatus, RequestStatus, Journey, ElevatorStatus, DestinationSet, HallCallQueue
- **3 Domain Services:** IScheduler, IElevatorRequestService, ITimeService
- **1 Application Service:** ElevatorSimulationService
- **1 Aggregate:** Building (aggregate root)

---

## 1. Entities (Have Identity, Mutable State)

### 1.1 Building (Aggregate Root)

**Identity:** Single instance per simulation

**Responsibility:** Manages collection of elevators and coordinates hall call assignments

**State:**
- Collection of elevators (fixed count, e.g., 4)
- Collection of pending hall calls (max 18 unique)
- Building configuration (floors, elevator count)

**Lifecycle:**
- Created at simulation startup
- Exists for entire simulation duration
- Destroyed at simulation shutdown

**Invariants:**
- Must have at least 1 elevator
- Must have at least 2 floors
- Number of elevators and floors fixed after creation
- All elevators belong to this building

---

### 1.2 Elevator (Entity)

**Identity:** Unique elevator ID (1, 2, 3, 4)

**Responsibility:** Manages own position, movement, and destination queue

**State:**
- **ID**: int (1-4)
- **CurrentFloor**: int (0-9 internal, displayed as 1-10)
- **State**: ElevatorState enum (IDLE, MOVING, STOPPED, LOADING)
- **Direction**: Direction enum (UP, DOWN, IDLE)
- **DestinationQueue**: Queue<int> (floors to visit in order)
- **AssignedHallCalls**: List<HallCall> (multiple hall calls being serviced)

**Lifecycle:**
- Created at building initialization
- Exists for entire simulation
- State changes: IDLE → MOVING → STOPPED → LOADING → IDLE

**Invariants:**
- CurrentFloor always in valid range [0, maxFloors-1]
- Cannot be MOVING without destinations
- Direction UP → next destination > currentFloor
- Direction DOWN → next destination < currentFloor
- Direction IDLE → no destinations or at destination
- DestinationQueue contains only valid floors
- All assigned hall calls must be in same direction as elevator
- All assigned hall calls must be between current floor and longest destination

**Hall Call Acceptance Rule:**
```csharp
// Elevator can accept hall calls between current position and longest destination
public bool CanAcceptHallCall(HallCall hallCall)
{
    if (Direction == Direction.IDLE) return true;
    if (hallCall.Direction != Direction) return false;
    
    int longestDestination = DestinationQueue.Max();
    
    if (Direction == Direction.UP)
        return hallCall.Floor > CurrentFloor && hallCall.Floor <= longestDestination;
    else
        return hallCall.Floor < CurrentFloor && hallCall.Floor >= longestDestination;
}
```

**State Transitions:**
```
IDLE → MOVING:      When assigned hall call with destination ≠ currentFloor
MOVING → STOPPED:   When reaches destination floor
STOPPED → LOADING:  After stop delay (elevator doors open)
LOADING → MOVING:   After loading delay, if more destinations remain
LOADING → IDLE:     After loading delay, if no more destinations
```

---

### 1.3 HallCall (Entity)

**Identity:** Composite key (Floor, Direction)

**Responsibility:** Represents a hall button press, accumulates destinations for same (floor, direction)

**State:**
- **Floor**: int (floor where hall button pressed, 0-9 internal)
- **Direction**: Direction enum (UP or DOWN, not IDLE)
- **Destinations**: HashSet<int> (accumulated destination floors)
- **Timestamp**: DateTime (when first created)
- **Status**: HallCallStatus enum (PENDING, ASSIGNED, COMPLETED)
- **AssignedElevatorId**: int? (which elevator assigned, nullable)

**Lifecycle:**
- Created when first request for (floor, direction) received
- PENDING → ASSIGNED when elevator assigned
- ASSIGNED → COMPLETED when elevator picks up passengers
- Removed from pending collection after completion

**Invariants:**
- Floor in valid range [0, maxFloors-1]
- Direction must be UP or DOWN (not IDLE)
- If Floor = 0 (bottom), Direction must be UP
- If Floor = maxFloors-1 (top), Direction must be DOWN
- Destinations non-empty (at least one destination)
- All destinations valid floors, none equal to source floor
- UP direction → all destinations > floor
- DOWN direction → all destinations < floor
- Status ASSIGNED → AssignedElevatorId not null
- Status PENDING → AssignedElevatorId null

---

### 1.4 Request (Entity)

**Identity:** Unique request ID (sequential int)

**Responsibility:** Represents an individual passenger's journey request

**State:**
- **ID**: int (unique, sequential)
- **Journey**: Journey (value object - source, destination, direction)
- **RequestTime**: DateTime (when request created)
- **Status**: RequestStatus enum (CREATED, ASSIGNED_TO_HALLCALL, COMPLETED)
- **AssignedHallCallId**: (Floor, Direction)? (which hall call this request belongs to)

**Lifecycle:**
- Created when user/generator requests elevator
- CREATED → ASSIGNED_TO_HALLCALL when added to hall call
- ASSIGNED_TO_HALLCALL → COMPLETED when journey finished
- Persists for metrics/logging after completion

**Invariants:**
- ID must be unique
- Journey must be valid (source ≠ destination)
- Status ASSIGNED_TO_HALLCALL → AssignedHallCallId not null
- Status CREATED → AssignedHallCallId null
- RequestTime cannot be in future

**Purpose:**
- Track individual passenger journeys (multiple passengers can share same hall call)
- Calculate per-request wait times
- Enable detailed metrics and logging

---

## 2. Value Objects (Immutable, No Identity)

### 2.1 Direction (Enum)

**Values:**
- `UP` - Moving upward (toward higher floors)
- `DOWN` - Moving downward (toward lower floors)
- `IDLE` - Not moving (stationary)

**Purpose:** Represents elevator movement direction

**Usage:**
- Elevator.Direction
- HallCall.Direction

**Derivation:**
```csharp
public static Direction DeriveDirection(int sourceFloor, int destinationFloor)
{
    if (destinationFloor > sourceFloor) return Direction.UP;
    if (destinationFloor < sourceFloor) return Direction.DOWN;
    throw new ArgumentException("Destination cannot equal source");
}
```

---

### 2.2 ElevatorState (Enum)

**Values:**
- `IDLE` - Not moving, no pending destinations
- `MOVING` - Traveling between floors
- `STOPPED` - Arrived at floor, doors closed
- `LOADING` - Doors open, passengers boarding/exiting

**Purpose:** Represents elevator operational state

**State Machine:**
```
    ┌─────┐
    │IDLE │←──────────────────┐
    └──┬──┘                   │
       │ assign               │
       ▼                      │
    ┌────────┐                │
    │MOVING  │                │
    └───┬────┘                │
        │ arrive              │
        ▼                     │
    ┌────────┐                │
    │STOPPED │                │
    └───┬────┘                │
        │ doors open          │
        ▼                     │
    ┌─────────┐               │
    │LOADING  │───────────────┘
    └─────────┘  no more stops
```

---

### 2.3 HallCallStatus (Enum)

**Values:**
- `PENDING` - Created, waiting for elevator assignment
- `ASSIGNED` - Elevator assigned, en route to pickup floor
- `COMPLETED` - Elevator arrived, passengers boarded

**Purpose:** Tracks hall call lifecycle

---

### 2.4 RequestStatus (Enum)

**Values:**
- `CREATED` - Request created, not yet assigned to hall call
- `ASSIGNED_TO_HALLCALL` - Request assigned to hall call
- `COMPLETED` - Journey completed (passenger delivered)

**Purpose:** Tracks request lifecycle

---

### 2.5 Journey (Value Object)

**Responsibility:** Encapsulates a passenger's journey details

**Properties:**
```csharp
public class Journey 
{
    public int SourceFloor { get; }
    public int DestinationFloor { get; }
    public Direction Direction { get; }  // Derived
    public DateTime RequestTime { get; }
    
    // Calculated properties
    public TimeSpan CalculateWaitTime(DateTime completionTime) 
        => completionTime - RequestTime;
    
    public int Distance => Math.Abs(DestinationFloor - SourceFloor);
    
    public Journey(int sourceFloor, int destinationFloor, DateTime requestTime)
    {
        if (sourceFloor == destinationFloor)
            throw new ArgumentException("Source cannot equal destination");
            
        SourceFloor = sourceFloor;
        DestinationFloor = destinationFloor;
        Direction = destinationFloor > sourceFloor ? Direction.UP : Direction.DOWN;
        RequestTime = requestTime;
    }
}
```

**Purpose:** Rich domain concept for passenger journey

---

### 2.6 ElevatorStatus (Value Object)

**Responsibility:** Immutable snapshot of elevator state for queries

**Properties:**
```csharp
public class ElevatorStatus 
{
    public int ElevatorId { get; }
    public int CurrentFloor { get; }
    public Direction Direction { get; }
    public ElevatorState State { get; }
    public IReadOnlyList<int> PendingStops { get; }
    public bool HasPassengers { get; }
    public IReadOnlyList<HallCall> AssignedHallCalls { get; }
    
    // Factory method
    public static ElevatorStatus FromElevator(Elevator elevator) 
    {
        return new ElevatorStatus(
            elevator.Id,
            elevator.CurrentFloor,
            elevator.Direction,
            elevator.State,
            elevator.DestinationQueue.ToList(),
            elevator.DestinationQueue.Any(),
            elevator.AssignedHallCalls.ToList()
        );
    }
}
```

**Purpose:** Query/display representation (DTO-like but domain concept)

---

### 2.7 DestinationSet (Value Object)

**Responsibility:** Encapsulates and validates destination collection

**Properties:**
```csharp
public class DestinationSet 
{
    private readonly HashSet<int> _destinations;
    private readonly int _sourceFloor;
    private readonly Direction _direction;
    
    public DestinationSet(int sourceFloor, Direction direction)
    {
        _sourceFloor = sourceFloor;
        _direction = direction;
        _destinations = new HashSet<int>();
    }
    
    public void AddDestination(int floor)
    {
        ValidateDestination(floor);
        _destinations.Add(floor);
    }
    
    private void ValidateDestination(int floor)
    {
        if (floor == _sourceFloor)
            throw new InvalidOperationException("Destination cannot equal source");
            
        if (_direction == Direction.UP && floor <= _sourceFloor)
            throw new InvalidOperationException("UP requires destination > source");
            
        if (_direction == Direction.DOWN && floor >= _sourceFloor)
            throw new InvalidOperationException("DOWN requires destination < source");
    }
    
    public IReadOnlySet<int> GetDestinations() => _destinations;
    public int Count => _destinations.Count;
    public bool IsEmpty => _destinations.Count == 0;
}
```

**Purpose:** Enforces destination validity invariants

---

### 2.8 HallCallQueue (Value Object)

**Responsibility:** Specialized collection for managing hall calls with idempotency

**Properties:**
```csharp
public class HallCallQueue 
{
    private readonly Dictionary<(int Floor, Direction Dir), HallCall> _hallCalls;
    
    public HallCallQueue()
    {
        _hallCalls = new Dictionary<(int, Direction), HallCall>();
    }
    
    // Idempotent add - returns existing or creates new
    public HallCall GetOrCreateHallCall(int floor, Direction direction)
    {
        var key = (floor, direction);
        if (!_hallCalls.TryGetValue(key, out var hallCall))
        {
            hallCall = new HallCall(floor, direction);
            _hallCalls[key] = hallCall;
        }
        return hallCall;
    }
    
    public void Remove(HallCall hallCall)
    {
        _hallCalls.Remove((hallCall.Floor, hallCall.Direction));
    }
    
    public IEnumerable<HallCall> GetPendingHallCalls()
        => _hallCalls.Values.Where(hc => hc.Status == HallCallStatus.PENDING);
    
    public int Count => _hallCalls.Count;
    public int MaxCapacity => 18;
    public bool IsFull => Count >= MaxCapacity;
}
```

**Purpose:** Encapsulates hall call deduplication logic

---

### 2.9 Floor (Value Object - Simple Type)

**Representation:** int (0-based internally)

**Display Conversion:**
```csharp
public static string ToDisplayFloor(int internalFloor) 
    => (internalFloor + 1).ToString();

public static int ToInternalFloor(int displayFloor) 
    => displayFloor - 1;
```

**Validation:**
```csharp
public static bool IsValid(int floor, int maxFloors) 
    => floor >= 0 && floor < maxFloors;
```

---

## 3. Domain Services (Stateless Logic)

### 3.1 IScheduler (Interface)

**Responsibility:** Selects optimal elevator for a hall call

**Method:**
```csharp
public interface IScheduler 
{
    Elevator? SelectElevator(IEnumerable<Elevator> elevators, HallCall hallCall);
}
```

**Implementations:**
- **NearestElevatorScheduler**: Selects closest idle/compatible elevator
- **ScanScheduler** (future): Implements SCAN (elevator) algorithm

**Pure Logic:** No side effects, same inputs → same output

---

### 3.2 IElevatorRequestService (Interface)

**Responsibility:** Handles incoming elevator requests

**Method:**
```csharp
public interface IElevatorRequestService 
{
    Request RequestElevator(int sourceFloor, int destinationFloor);
    IEnumerable<ElevatorStatus> GetAllElevatorStatuses();
    ElevatorStatus GetElevatorStatus(int elevatorId);
}
```

---

### 3.3 ITimeService (Interface)

**Responsibility:** Time abstraction for testable simulation

**Method:**
```csharp
public interface ITimeService 
{
    Task DelayAsync(int milliseconds, CancellationToken ct);
    DateTime GetCurrentTime();
}
```

**Implementations:**
- **RealTimeService**: Uses actual Task.Delay (production)
- **FastTimeService**: Accelerated delays for testing (100x faster)
- **FakeTimeService**: Instant delays for unit tests

**Purpose:** 
- Keeps domain testable (no real delays in tests)
- Allows configurable simulation speed
- Separates timing from domain logic

---

### 3.4 IElevatorMovementCoordinator (Interface)

**Responsibility:** Determines next action for elevator (pure domain logic)

**Method:**
```csharp
public interface IElevatorMovementCoordinator 
{
    MovementCommand GetNextMovementCommand(Elevator elevator);
}

public class MovementCommand 
{
    public MovementAction Action { get; }  
    // MOVE_UP, MOVE_DOWN, STOP, LOAD, IDLE
    public int? TargetFloor { get; }
}

public enum MovementAction 
{
    MOVE_UP,
    MOVE_DOWN,
    STOP,
    LOAD,
    IDLE
}
```

**Purpose:** 
- Pure domain logic (what should elevator do)
- No timing, no I/O
- Easily testable

---

## 4. Application Services (Orchestration)

### 4.1 ElevatorSimulationService

**Responsibility:** Orchestrates elevator execution with timing

**NOT a domain service** - lives in application layer

**Method:**
```csharp
public class ElevatorSimulationService 
{
    private readonly IElevatorMovementCoordinator _coordinator;
    private readonly ITimeService _timeService;
    private readonly int _movementTimeMs;
    private readonly int _loadingTimeMs;
    
    public async Task RunElevatorAsync(Elevator elevator, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var command = _coordinator.GetNextMovementCommand(elevator);
            
            switch (command.Action)
            {
                case MovementAction.MOVE_UP:
                    elevator.MoveUp();
                    await _timeService.DelayAsync(_movementTimeMs, ct);
                    break;
                    
                case MovementAction.MOVE_DOWN:
                    elevator.MoveDown();
                    await _timeService.DelayAsync(_movementTimeMs, ct);
                    break;
                    
                case MovementAction.STOP:
                    elevator.Stop();
                    await _timeService.DelayAsync(_stopTimeMs, ct);
                    break;
                    
                case MovementAction.LOAD:
                    elevator.Load();
                    await _timeService.DelayAsync(_loadingTimeMs, ct);
                    break;
                    
                case MovementAction.IDLE:
                    await _timeService.DelayAsync(1000, ct);
                    break;
            }
        }
    }
}
```

**Purpose:**
- Handles HOW and WHEN (timing, coordination)
- Uses domain services for WHAT (logic)
- Orchestration layer between domain and infrastructure

---

## 5. Relationships

### 5.1 Building → Elevator (Composition, 1-to-Many)

```
Building (1) ──────< owns >────── Elevator (4)
```

- Building owns 4 elevators
- Elevators cannot exist without Building
- Building lifecycle controls Elevator lifecycle

---

### 5.2 Building → HallCallQueue (Composition, 1-to-1)

```
Building (1) ──────< owns >────── HallCallQueue (1)
```

- Building owns one hall call queue
- Queue manages 0-18 hall calls
- Queue lifecycle tied to Building

---

### 5.3 Building → Request (Aggregation, 1-to-Many)

```
Building (1) ──────< receives >────── Request (N)
```

- Building receives all requests
- Requests can be tracked independently
- Building coordinates request-to-hall call assignment

---

### 5.4 HallCallQueue → HallCall (Aggregation, 1-to-Many)

```
HallCallQueue (1) ──────< contains >────── HallCall (0-18)
```

- Queue contains 0-18 pending hall calls
- Hall calls deduplicated by (floor, direction)
- Queue enforces max capacity

---

### 5.5 Request → Journey (Composition, 1-to-1)

```
Request (1) ──────< contains >────── Journey (1)
```

- Each request contains one journey
- Journey is immutable value object
- Journey lifecycle tied to Request

---

### 5.6 Request → HallCall (Association, Many-to-One)

```
Request (N) ──────< assigned to >────── HallCall (1)
```

- Multiple requests can map to same hall call
- Request knows which hall call it belongs to
- Hall call doesn't track individual requests (just destinations)

---

### 5.7 Elevator → HallCall (Association, One-to-Many)

```
Elevator (1) ──────< assigned to >────── HallCall (0-N)
```

- Elevator can be assigned to multiple hall calls simultaneously
- Hall calls must be in same direction as elevator
- Hall calls must be between current floor and longest destination
- Hall call can be assigned to exactly 1 elevator

---

### 5.8 HallCall → DestinationSet (Composition, 1-to-1)

```
HallCall (1) ──────< contains >────── DestinationSet (1)
```

- Hall call contains one destination set
- Destination set manages 1-N destination floors
- Destination set enforces direction validity

---

## 6. Domain Invariants

### System-Level Invariants

1. **Unique Hall Calls**: No two pending hall calls with same (floor, direction)
2. **No Lost Requests**: Every hall call is PENDING, ASSIGNED, or COMPLETED
3. **Assignment Exclusivity**: Each hall call assigned to at most one elevator
4. **Bounded Hall Calls**: Maximum 18 concurrent pending hall calls
5. **Floor Validity**: All floors in range [0, maxFloors-1] internally

---

### Elevator Invariants

1. **Floor Range**: `0 <= CurrentFloor < maxFloors`
2. **State Consistency**: 
   - IDLE → no destinations OR at all destinations
   - MOVING → has destinations AND not at next destination
   - STOPPED → at destination floor
   - LOADING → at destination floor, doors open
3. **Direction Consistency**:
   - UP → next destination > currentFloor
   - DOWN → next destination < currentFloor
   - IDLE → no destinations OR at destination
4. **Movement Rule**: Elevator moves one floor at a time (no skipping)
5. **Time Rule**: Each floor transition takes configured time

---

### HallCall Invariants

1. **Direction Validity**:
   - Floor 0 (bottom) → Direction must be UP
   - Floor maxFloors-1 (top) → Direction must be DOWN
   - Middle floors → can be UP or DOWN
2. **Destination Validity**:
   - UP → all destinations > floor
   - DOWN → all destinations < floor
   - No destination equals floor
3. **Status Consistency**:
   - PENDING → AssignedElevatorId is null
   - ASSIGNED → AssignedElevatorId is not null
   - COMPLETED → removed from pending collection
4. **Non-Empty Destinations**: Destinations.Count >= 1

---

## 6. Domain Events (Things That Happened)

### 6.1 HallCallCreated

**When:** New hall call created (first request for floor+direction)

**Data:**
- HallCallId (floor, direction)
- Timestamp
- Initial destination

---

### 6.2 DestinationAdded

**When:** Additional destination added to existing hall call

**Data:**
- HallCallId (floor, direction)
- New destination floor
- Updated destination count

---

### 6.3 HallCallAssigned

**When:** Elevator assigned to hall call

**Data:**
- HallCallId
- ElevatorId
- Estimated arrival time

---

### 6.4 ElevatorStateChanged

**When:** Elevator transitions between states

**Data:**
- ElevatorId
- OldState
- NewState
- CurrentFloor
- Timestamp

---

### 6.5 ElevatorMoved

**When:** Elevator moves from one floor to another

**Data:**
- ElevatorId
- FromFloor
- ToFloor
- Direction
- Timestamp

---

### 6.6 ElevatorArrived

**When:** Elevator reaches destination floor

**Data:**
- ElevatorId
- Floor
- HallCallId (if picking up)
- PassengerCount (destinations to serve)

---

### 6.7 PassengersBoarded

**When:** Passengers board elevator (loading complete)

**Data:**
- ElevatorId
- Floor
- Destinations added to elevator queue
- Timestamp

---

### 6.8 HallCallCompleted

**When:** Elevator picked up all passengers for hall call

**Data:**
- HallCallId
- ElevatorId
- WaitTime (timestamp difference)
- Timestamp

---

## 7. Bounded Contexts

For this simple system, we have essentially **one bounded context**: **Elevator Control**

**Future contexts (if system expands):**
- **Building Management** (multiple buildings)
- **Monitoring & Analytics** (historical data, metrics)
- **Maintenance** (elevator service schedules)

---

## 8. Lifecycle States

### 8.1 Elevator Lifecycle

```
[Created] → [IDLE] → [MOVING] → [STOPPED] → [LOADING] → [IDLE] → ... → [Destroyed]
            ↑_______________________________________________|
```

**Triggers:**
- Created → IDLE: Initialization
- IDLE → MOVING: Hall call assigned
- MOVING → STOPPED: Reached destination
- STOPPED → LOADING: Stop delay elapsed
- LOADING → IDLE: No more destinations
- LOADING → MOVING: More destinations remain

---

### 8.2 HallCall Lifecycle

```
[Created] → [PENDING] → [ASSIGNED] → [COMPLETED] → [Removed]
```

**Triggers:**
- Created → PENDING: First request for (floor, direction)
- PENDING → ASSIGNED: Elevator assigned
- ASSIGNED → COMPLETED: Elevator arrived and passengers boarded
- COMPLETED → Removed: Removed from pending collection

---

## 9. Aggregates

### 9.1 Building Aggregate

**Aggregate Root:** Building

**Contains:**
- Elevators (entities)
- Pending HallCalls (entities)
- Configuration (value object)

**Consistency Boundary:**
- All modifications to elevators go through Building
- All hall call assignments go through Building
- Ensures invariants maintained

**Operations:**
- RequestElevator(source, destination)
- AssignHallCall(hallCall, elevator)
- UpdateElevatorState(elevatorId, newState)
- GetElevatorStatus(elevatorId)

---

## 10. Domain Model Summary

### Entities
| Entity | Identity | Key State | Mutable |
|--------|----------|-----------|---------|
| Building | Singleton | Elevators, HallCallQueue, Requests | Yes |
| Elevator | ElevatorId (int) | Floor, State, Direction, Queue, AssignedHallCalls | Yes |
| HallCall | (Floor, Direction) | DestinationSet, Status, AssignedElevatorId | Yes |
| Request | RequestId (int) | Journey, Status, AssignedHallCallId | Yes |

### Value Objects
| Value Object | Type | Immutable | Purpose |
|--------------|------|-----------|---------|
| Direction | Enum | Yes | Movement direction |
| ElevatorState | Enum | Yes | Operational state |
| HallCallStatus | Enum | Yes | Hall call lifecycle |
| RequestStatus | Enum | Yes | Request lifecycle |
| Journey | Class | Yes | Passenger journey details |
| ElevatorStatus | Class | Yes | Elevator state snapshot |
| DestinationSet | Class | Mutable | Validated destination collection |
| HallCallQueue | Class | Mutable | Hall call deduplication |
| Floor | int | Yes | Floor number |

### Domain Services
| Service | Stateless | Purpose |
|---------|-----------|---------|
| IScheduler | Yes | Select optimal elevator |
| IElevatorRequestService | Yes | Handle requests |
| ITimeService | Yes | Time abstraction |
| IElevatorMovementCoordinator | Yes | Determine next elevator action |

### Application Services
| Service | Purpose |
|---------|---------|
| ElevatorSimulationService | Orchestrate elevator execution with timing |

### Relationships
- Building **owns** Elevators (1-to-4 composition)
- Building **owns** HallCallQueue (1-to-1 composition)
- Building **receives** Requests (1-to-many aggregation)
- HallCallQueue **contains** HallCalls (1-to-18 aggregation)
- Request **contains** Journey (1-to-1 composition)
- Request **assigned to** HallCall (many-to-1 association)
- Elevator **assigned to** HallCalls (1-to-many association)
- HallCall **contains** DestinationSet (1-to-1 composition)

### Key Invariants
- Maximum 18 concurrent hall calls (enforced by HallCallQueue)
- Elevators move one floor at a time
- Direction matches destination relationship
- No lost or duplicate assignments
- Floor validity enforced
- Elevator can only accept hall calls in same direction
- Elevator can only accept hall calls between current floor and longest destination
- Multiple requests can map to same hall call
- Each request tracks individual journey for metrics

---

## Next Steps

This domain model provides the foundation for:
- **Phase 4**: Responsibilities (what each entity owns/doesn't own)
- **Phase 10**: Low-Level Design (classes, interfaces, patterns)
- **Phase 11**: Implementation (actual C# code)

The model is simple, clear, and directly maps to the problem domain without over-engineering.
