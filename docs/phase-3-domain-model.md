# Phase 3 - Domain Model

## Core Concepts Overview

The domain model identifies the key concepts, their relationships, and invariants without diving into implementation details.

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
- **AssignedHallCall**: HallCall? (currently servicing, nullable)

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

### 2.4 Floor (Value Object - Simple Type)

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
    void RequestElevator(int sourceFloor, int destinationFloor);
    IEnumerable<ElevatorStatus> GetAllElevatorStatuses();
    ElevatorStatus GetElevatorStatus(int elevatorId);
}
```

---

## 4. Relationships

### 4.1 Building → Elevator (Composition, 1-to-Many)

```
Building (1) ──────< owns >────── Elevator (4)
```

- Building owns 4 elevators
- Elevators cannot exist without Building
- Building lifecycle controls Elevator lifecycle

---

### 4.2 Building → HallCall (Aggregation, 1-to-Many)

```
Building (1) ──────< manages >────── HallCall (0-18)
```

- Building manages 0-18 pending hall calls
- Hall calls can exist independently (value-like)
- Building removes hall calls when completed

---

### 4.3 Elevator → HallCall (Association, Many-to-One)

```
Elevator (1) ──────< assigned to >────── HallCall (0-1)
```

- Elevator can be assigned to at most 1 hall call at a time
- Hall call can be assigned to exactly 1 elevator
- Bidirectional: Elevator knows hall call, hall call knows elevator ID

---

### 4.4 HallCall → Destinations (Value Composition)

```
HallCall (1) ──────< contains >────── Destinations (1-N)
```

- Hall call contains 1 or more destination floors
- Destinations stored as HashSet (deduplicated)
- Adding same destination multiple times has no effect

---

## 5. Domain Invariants

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
| Building | Singleton | Elevators, HallCalls | Yes |
| Elevator | ElevatorId (int) | Floor, State, Direction, Queue | Yes |
| HallCall | (Floor, Direction) | Destinations, Status, Assignment | Yes |

### Value Objects
| Value Object | Type | Immutable | Purpose |
|--------------|------|-----------|---------|
| Direction | Enum | Yes | Movement direction |
| ElevatorState | Enum | Yes | Operational state |
| HallCallStatus | Enum | Yes | Request lifecycle |
| Floor | int | Yes | Floor number |

### Domain Services
| Service | Stateless | Purpose |
|---------|-----------|---------|
| IScheduler | Yes | Select optimal elevator |
| IElevatorRequestService | Yes | Handle requests |

### Relationships
- Building **owns** Elevators (1-to-4 composition)
- Building **manages** HallCalls (1-to-many aggregation)
- Elevator **assigned to** HallCall (many-to-one association)
- HallCall **contains** Destinations (1-to-many composition)

### Key Invariants
- Maximum 18 concurrent hall calls
- Elevators move one floor at a time
- Direction matches destination relationship
- No lost or duplicate assignments
- Floor validity enforced

---

## Next Steps

This domain model provides the foundation for:
- **Phase 4**: Responsibilities (what each entity owns/doesn't own)
- **Phase 10**: Low-Level Design (classes, interfaces, patterns)
- **Phase 11**: Implementation (actual C# code)

The model is simple, clear, and directly maps to the problem domain without over-engineering.
