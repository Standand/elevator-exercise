# Phase 5 - High-Level Design

## Overview

This phase defines the major components, their interactions, data flows, and communication patterns.

**Key Principle:** Components interact through well-defined interfaces. Data flows in clear, predictable paths.

---

## System Architecture

### Layer Architecture (Clean Architecture)

```
┌─────────────────────────────────────────────────────────────┐
│                    Application Layer                        │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐  │
│  │           SystemOrchestrator                         │  │
│  │  - BootUp(): Initialize system                       │  │
│  │  - Shutdown(): Graceful shutdown                     │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐  │
│  │       ElevatorSimulationService                      │  │
│  │  - RunAsync(): Orchestrate simulation loop           │  │
│  │  - Calls Building.ProcessTick() every interval       │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐  │
│  │       RandomRequestGenerator                         │  │
│  │  - GenerateAsync(): Create random requests           │  │
│  │  - Calls IElevatorRequestService                     │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                             │
└─────────────────────────────────────────────────────────────┘
                            ↓ uses ↓
┌─────────────────────────────────────────────────────────────┐
│                      Domain Layer                           │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐  │
│  │        IElevatorRequestService (Domain Service)      │  │
│  │  - ProcessRequestAsync(source, dest)                 │  │
│  │  - GetAllElevatorStatusAsync()                       │  │
│  └──────────────────────────────────────────────────────┘  │
│                            ↓ uses ↓                         │
│  ┌──────────────────────────────────────────────────────┐  │
│  │           Building (Aggregate Root)                  │  │
│  │  - ProcessRequest(request): Assign to elevator       │  │
│  │  - ProcessTick(): Move all elevators                 │  │
│  │  - GetAllElevatorStatus(): Query state               │  │
│  │  - Owns: Elevators, HallCallQueue, Requests          │  │
│  │  - Provides: Global lock for consistency             │  │
│  └──────────────────────────────────────────────────────┘  │
│                            ↓ owns ↓                         │
│  ┌─────────┬─────────┬─────────┬─────────┐                │
│  │Elevator1│Elevator2│Elevator3│Elevator4│                │
│  │ Floor:5 │ Floor:8 │ Floor:2 │ Floor:1 │                │
│  │ Dir: UP │Dir:DOWN │Dir: UP  │Dir:IDLE │                │
│  └─────────┴─────────┴─────────┴─────────┘                │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐  │
│  │           HallCallQueue (Value Object)               │  │
│  │  - GetOrCreate(floor, direction): Idempotent         │  │
│  │  - Max 18 concurrent hall calls                      │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐  │
│  │         Domain Services (Stateless)                  │  │
│  │  - IScheduler: Select optimal elevator               │  │
│  │  - IElevatorMovementCoordinator: Decide actions      │  │
│  │  - ITimeService: Abstract time                       │  │
│  │  - ILogger: Logging abstraction                      │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                             │
└─────────────────────────────────────────────────────────────┘
                            ↓ uses ↓
┌─────────────────────────────────────────────────────────────┐
│                  Infrastructure Layer                       │
│                                                             │
│  - ConsoleLogger (implements ILogger)                       │
│  - RealTimeService (implements ITimeService)                │
│  - FastTimeService (implements ITimeService - testing)      │
│  - SimulationConfiguration (loads from JSON)                │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## Communication Patterns

### 1. Synchronous vs Asynchronous

| Communication Path | Pattern | Reason |
|-------------------|---------|--------|
| RandomRequestGenerator → IElevatorRequestService | **Async** | External entry point, non-blocking |
| IElevatorRequestService → Building | **Sync** | Domain operation within lock |
| ElevatorSimulationService → Building.ProcessTick() | **Sync** | Domain operation within lock |
| Building → Elevator | **Sync** | Internal aggregate operations |
| Building → IScheduler | **Sync** | Pure function, no I/O |
| Building → IElevatorMovementCoordinator | **Sync** | Pure function, no I/O |
| Any component → ITimeService.DelayAsync() | **Async** | Simulated time delay |
| Any component → ILogger | **Sync** | Console logging (fast) |

### 2. Direct Method Calls (No Messaging)

**Decision:** Keep it simple with direct method calls for Phase 1.

- No message queues
- No event bus (events logged directly)
- No CQRS (Command Query Responsibility Segregation)

**Rationale:** 
- Single-process application
- Strong consistency requirement
- Simplicity > distributed complexity

---

## Data Flow Diagrams

### Flow 1: Request Processing

```
┌─────────────────────────────────────────────────────────────┐
│ 1. RandomRequestGenerator                                   │
│    - Generates: source=5, destination=8                     │
│    - Every 5 seconds (configurable)                         │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ 2. IElevatorRequestService.ProcessRequestAsync(5, 8)        │
│    - Create Request entity (ID, Journey, Timestamp)         │
│    - Log: "Request created: ID=abc, 5→8"                    │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ 3. Building.ProcessRequest(request)                         │
│    ┌─────────────────────────────────────────────────────┐ │
│    │ LOCK ACQUIRED                                       │ │
│    │                                                     │ │
│    │ a) Validate floors (1 to maxFloor)                 │ │
│    │    - Throw ArgumentException if invalid            │ │
│    │                                                     │ │
│    │ b) HallCallQueue.GetOrCreate(floor=5, dir=UP)      │ │
│    │    - Returns existing or creates new               │ │
│    │    - Add destination 8 to HallCall                 │ │
│    │    - Log: "HallCall created/updated: Floor 5 UP"   │ │
│    │                                                     │ │
│    │ c) IScheduler.SelectElevator(hallCall, elevators)  │ │
│    │    - Direction-aware algorithm                     │ │
│    │    - Returns best elevator (or null)               │ │
│    │                                                     │ │
│    │ d) If elevator found:                              │ │
│    │    - Elevator.AssignHallCall(hallCall)             │ │
│    │    - HallCall.MarkAsAssigned(elevatorId)           │ │
│    │    - Log: "HallCall assigned: Elevator 2"          │ │
│    │    Else:                                           │ │
│    │    - HallCall stays PENDING                        │ │
│    │    - Will retry on next tick                       │ │
│    │    - Log: "No elevator available, pending"         │ │
│    │                                                     │ │
│    │ e) Request.MarkAsAssignedToHallCall(hallCallId)    │ │
│    │    - Log: "Request assigned to HallCall"           │ │
│    │                                                     │ │
│    │ f) Publish events:                                 │ │
│    │    - RequestCreated                                │ │
│    │    - HallCallCreated (if new)                      │ │
│    │    - HallCallAssigned (if assigned)                │ │
│    │                                                     │ │
│    │ LOCK RELEASED                                       │ │
│    └─────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ 4. Return success/failure to caller                         │
│    - Success: Request accepted                              │
│    - Failure: Invalid floors, queue full, etc.              │
└─────────────────────────────────────────────────────────────┘
```

**Key Points:**
- Single lock acquisition (entire operation atomic)
- Hall call can stay PENDING if no elevator available
- Request always created (even if not immediately assigned)

---

### Flow 2: Simulation Tick (Elevator Movement)

```
┌─────────────────────────────────────────────────────────────┐
│ ElevatorSimulationService.RunAsync()                        │
│                                                             │
│ while (!cancellationToken.IsCancellationRequested)          │
│ {                                                           │
│     Building.ProcessTick();                                 │
│     await ITimeService.DelayAsync(tickInterval);            │
│ }                                                           │
└─────────────────────────────────────────────────────────────┘
                            ↓ every 1 second
┌─────────────────────────────────────────────────────────────┐
│ Building.ProcessTick()                                      │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ LOCK ACQUIRED                                           │ │
│ │                                                         │ │
│ │ 1. Retry PENDING hall calls (FIFO order)                │ │
│ │    var pending = HallCallQueue.GetPending()             │ │
│ │                                .OrderBy(hc => CreatedAt)│ │
│ │    foreach (hallCall in pending)                        │ │
│ │    {                                                    │ │
│ │        elevator = IScheduler.SelectElevator(hallCall);  │ │
│ │        if (elevator != null)                            │ │
│ │        {                                                │ │
│ │            AssignHallCall(elevator, hallCall);          │ │
│ │            Log: "HallCall assigned to Elevator"         │ │
│ │        }                                                │ │
│ │        else                                             │ │
│ │        {                                                │ │
│ │            // Remains PENDING, retry next tick          │ │
│ │            Log: "No elevator available"                 │ │
│ │        }                                                │ │
│ │    }                                                    │ │
│ │                                                         │ │
│ │ 2. Process each elevator (FIXED ORDER: 1, 2, 3, 4)     │ │
│ │    foreach (elevator in _elevators)                     │ │
│ │    {                                                    │ │
│ │        ┌─────────────────────────────────────────────┐ │ │
│ │        │ a) Handle LOADING elevators (door timer)    │ │ │
│ │        │    if (elevator.State == LOADING)           │ │ │
│ │        │    {                                        │ │ │
│ │        │        elevator.DecrementDoorTimer();       │ │ │
│ │        │        if (elevator.DoorTimerExpired)       │ │ │
│ │        │        {                                    │ │ │
│ │        │            CompleteHallCallsAtFloor();      │ │ │
│ │        │            ExecuteAction(CLOSE_DOORS);      │ │ │
│ │        │            continue; // Skip rest           │ │ │
│ │        │        }                                    │ │ │
│ │        │        else continue; // Doors still open   │ │ │
│ │        │    }                                        │ │ │
│ │        └─────────────────────────────────────────────┘ │ │
│ │                                                         │ │
│ │        ┌─────────────────────────────────────────────┐ │ │
│ │        │ b) Decide next action (non-LOADING)        │ │ │
│ │        │    action = IElevatorMovementCoordinator    │ │ │
│ │        │             .DecideNextAction(elevator)     │ │ │
│ │        │                                             │ │ │
│ │        │    Returns: MOVE_UP, MOVE_DOWN,            │ │ │
│ │        │             OPEN_DOORS, CLOSE_DOORS, IDLE  │ │ │
│ │        └─────────────────────────────────────────────┘ │ │
│ │                                                         │ │
│ │        ┌─────────────────────────────────────────────┐ │ │
│ │        │ c) Execute action                           │ │ │
│ │        │    elevator.ExecuteAction(action)           │ │ │
│ │        │                                             │ │ │
│ │        │    - MOVE_UP: currentFloor++               │ │ │
│ │        │    - MOVE_DOWN: currentFloor--             │ │ │
│ │        │    - OPEN_DOORS:                            │ │ │
│ │        │        state = LOADING                      │ │ │
│ │        │        Remove currentFloor from destinations│ │ │
│ │        │        doorTimer = doorOpenDurationSeconds  │ │ │
│ │        │    - CLOSE_DOORS: state = MOVING/IDLE      │ │ │
│ │        │    - IDLE: no-op                            │ │ │
│ │        │                                             │ │ │
│ │        │    Log: "Elevator 2 moved to floor 6"      │ │ │
│ │        │    Publish: ElevatorMoved event            │ │ │
│ │        └─────────────────────────────────────────────┘ │ │
│ │    }                                                    │ │
│ │                                                         │ │
│ │ 3. Log current state of all elevators                   │ │
│ │    Log: "Tick N: E1[F5,UP], E2[F8,DOWN], ..."          │ │
│ │                                                         │ │
│ │ LOCK RELEASED                                           │ │
│ └─────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

**Key Points:**
- Single lock for entire tick (all elevators process atomically)
- Fixed order: Elevator 1, 2, 3, 4 (deterministic)
- Retry pending hall calls each tick (FIFO order by creation time)
- Door timer decrements BEFORE DecideNextAction() for LOADING elevators
- Destination removed when doors OPEN (STOPPED → LOADING)
- Hall call completed when doors START CLOSING (during CLOSE_DOORS action)

**Detailed Hall Call Completion:**
```
CompleteHallCallsAtFloor(elevator):
    foreach (hallCall in elevator.AssignedHallCalls)
    {
        if (elevator.CurrentFloor == hallCall.Floor &&
            elevator.Direction == hallCall.Direction &&
            elevator.State == LOADING &&
            elevator.DoorTimerExpired)
        {
            // Mark hall call as completed
            hallCall.MarkAsCompleted();
            elevator.RemoveHallCall(hallCall);
            _hallCallQueue.Remove(hallCall);
            Log: "HallCall completed";
            Publish: HallCallCompleted;
            
            // Complete all requests in this hall call
            foreach (request in _requests.Where(r => r.AssignedHallCallId == hallCall.Id))
            {
                request.MarkAsCompleted(DateTime.UtcNow);
                Log: $"Request {request.Id} fulfilled, WaitTime={request.WaitTime}";
                Publish: RequestFulfilled;
            }
        }
    }
```

---

### Flow 3: Status Query

```
┌─────────────────────────────────────────────────────────────┐
│ Console/User requests status                                │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ IElevatorRequestService.GetAllElevatorStatusAsync()         │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ Building.GetAllElevatorStatus()                             │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ LOCK ACQUIRED                                           │ │
│ │                                                         │ │
│ │ foreach (elevator in _elevators)                        │ │
│ │ {                                                       │ │
│ │     status = new ElevatorStatus(                        │ │
│ │         Id: elevator.Id,                                │ │
│ │         CurrentFloor: elevator.CurrentFloor,            │ │
│ │         Direction: elevator.Direction,                  │ │
│ │         State: elevator.State,                          │ │
│ │         Destinations: elevator.Destinations.ToList(),   │ │
│ │         AssignedHallCalls: elevator.HallCalls.ToList(), │ │
│ │         HasPassengers: elevator.HasPassengers           │ │
│ │     );                                                  │ │
│ │     statusList.Add(status);                             │ │
│ │ }                                                       │ │
│ │                                                         │ │
│ │ LOCK RELEASED                                           │ │
│ └─────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ Return List<ElevatorStatus> (immutable snapshot)            │
└─────────────────────────────────────────────────────────────┘
```

**Key Points:**
- Read operation still requires lock (consistent snapshot)
- Returns immutable value objects (thread-safe)
- No caching (always fresh data)

---

## Elevator Selection Algorithm (IScheduler)

### Direction-Aware Scheduling

**Algorithm:** Select elevator that minimizes response time while respecting direction.

```
IScheduler.SelectElevator(HallCall hallCall, List<Elevator> elevators)
{
    Elevator bestCandidate = null;
    int bestScore = int.MaxValue;
    
    foreach (var elevator in elevators)
    {
        // Skip if can't accept
        if (!elevator.CanAcceptHallCall(hallCall))
            continue;
        
        int score = CalculateScore(elevator, hallCall);
        
        if (score < bestScore)
        {
            bestScore = score;
            bestCandidate = elevator;
        }
    }
    
    return bestCandidate; // Can be null if none suitable
}
```

### Scoring Logic

```
CalculateScore(Elevator elevator, HallCall hallCall)
{
    int distance = |elevator.CurrentFloor - hallCall.Floor|;
    
    // Case 1: IDLE elevator
    if (elevator.State == IDLE)
    {
        return distance; // Just distance
    }
    
    // Case 2: Going SAME direction AND will pass this floor
    if (elevator.Direction == hallCall.Direction)
    {
        bool willPass = CheckIfWillPass(elevator, hallCall);
        
        if (willPass)
        {
            return distance; // Best case - en route pickup
        }
    }
    
    // Case 3: Going OPPOSITE direction or won't pass
    // High penalty to deprioritize
    return 1000 + distance;
}

bool CheckIfWillPass(Elevator elevator, HallCall hallCall)
{
    if (hallCall.Direction == UP)
    {
        return elevator.CurrentFloor < hallCall.Floor &&
               hallCall.Floor <= elevator.FurthestDestination;
    }
    else // DOWN
    {
        return elevator.CurrentFloor > hallCall.Floor &&
               hallCall.Floor >= elevator.FurthestDestination;
    }
}
```

### Elevator.CanAcceptHallCall() Logic

```
bool CanAcceptHallCall(HallCall hallCall)
{
    // Rule 1: If at the hall call floor in LOADING state, CANNOT accept
    // (Elevator already servicing this floor, don't accept duplicate calls)
    if (CurrentFloor == hallCall.Floor && 
        Direction == hallCall.Direction && 
        State == LOADING)
    {
        return false; // Already at this floor with doors open
    }
    
    // Rule 2: If IDLE, can accept any hall call
    if (State == IDLE)
        return true;
    
    // Rule 3: If MOVING, must be same direction
    if (Direction != hallCall.Direction)
        return false;
    
    // Rule 4: Hall call floor must be between current and furthest destination
    // AND not equal to current floor (already checked in Rule 1)
    if (Direction == UP)
    {
        return CurrentFloor < hallCall.Floor && 
               hallCall.Floor <= FurthestDestination;
    }
    else // DOWN
    {
        return CurrentFloor > hallCall.Floor && 
               hallCall.Floor >= FurthestDestination;
    }
}
```

### Examples

**Example 1: IDLE Elevator Wins**
```
HallCall: Floor 5, UP

Elevator 1: Floor 3, IDLE
  → CanAccept: YES
  → Score: |3-5| = 2 ✓ SELECTED

Elevator 2: Floor 8, going DOWN to floor 2
  → CanAccept: NO (wrong direction)
  → Score: N/A

Elevator 3: Floor 10, going UP to floor 15
  → CanAccept: NO (already past floor 5)
  → Score: N/A
```

**Example 2: En-Route Pickup**
```
HallCall: Floor 7, UP

Elevator 1: Floor 3, IDLE
  → CanAccept: YES
  → Score: |3-7| = 4

Elevator 2: Floor 5, going UP to floor 10
  → CanAccept: YES (will pass floor 7)
  → Score: |5-7| = 2 ✓ SELECTED (en route)

Elevator 3: Floor 8, going DOWN
  → CanAccept: NO (wrong direction)
```

**Example 3: No Elevator Available**
```
HallCall: Floor 5, UP

Elevator 1: Floor 8, going DOWN to floor 2
  → CanAccept: NO (wrong direction)

Elevator 2: Floor 3, going UP to floor 4
  → CanAccept: NO (won't reach floor 5)

Elevator 3: Floor 10, going DOWN to floor 6
  → CanAccept: NO (wrong direction)

Elevator 4: Floor 2, going UP to floor 3
  → CanAccept: NO (won't reach floor 5)

Result: Return null, hall call stays PENDING
```

### Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| **Hall calls per elevator** | No limit | Simpler, max 18 hall calls total anyway |
| **No elevator available** | Return null, retry later | Hall calls naturally get assigned when elevators align |
| **Load balancing** | Not implemented | Keep simple, distance + direction sufficient |
| **Scoring complexity** | Simple (distance + penalty) | Avoid premature optimization |

---

## Elevator Movement Coordination

### IElevatorMovementCoordinator.DecideNextAction()

**Purpose:** Decide what an elevator should do on this tick (pure logic, stateless).

```
DecideNextAction(Elevator elevator)
{
    // Case 1: No destinations → IDLE
    if (elevator.Destinations.IsEmpty())
    {
        return ElevatorAction.IDLE;
    }
    
    // Case 2: At a destination → OPEN_DOORS
    if (elevator.Destinations.Contains(elevator.CurrentFloor))
    {
        if (elevator.State == LOADING)
        {
            // Doors already open, close them
            return ElevatorAction.CLOSE_DOORS;
        }
        else
        {
            // Arrived at floor, open doors
            return ElevatorAction.OPEN_DOORS;
        }
    }
    
    // Case 3: Need to move toward next destination
    int nextDestination = elevator.GetNextDestination();
    
    if (nextDestination > elevator.CurrentFloor)
    {
        return ElevatorAction.MOVE_UP;
    }
    else if (nextDestination < elevator.CurrentFloor)
    {
        return ElevatorAction.MOVE_DOWN;
    }
    else
    {
        return ElevatorAction.IDLE; // Shouldn't happen
    }
}
```

### Elevator Actions

| Action | Effect | State Transition |
|--------|--------|------------------|
| **MOVE_UP** | CurrentFloor++ | MOVING, Direction=UP |
| **MOVE_DOWN** | CurrentFloor-- | MOVING, Direction=DOWN |
| **OPEN_DOORS** | Doors open | STOPPED → LOADING |
| **CLOSE_DOORS** | Doors close | LOADING → MOVING or IDLE |
| **IDLE** | No-op | IDLE, Direction=IDLE |

---

## State Management

### Where State Lives

```
Building (Aggregate Root)
├── List<Elevator> _elevators (4 instances)
│   ├── Elevator 1: { CurrentFloor, Direction, State, Destinations, HallCalls }
│   ├── Elevator 2: { ... }
│   ├── Elevator 3: { ... }
│   └── Elevator 4: { ... }
│
├── HallCallQueue _hallCallQueue
│   └── Dictionary<(Floor, Direction), HallCall>
│       └── Max 18 entries
│
└── List<Request> _requests
    └── All requests (for metrics/tracking)
```

### State Persistence

**Phase 1 Decision:** In-memory only, no persistence.

- State lost on restart (acceptable per requirements)
- No database
- No file storage
- Fresh start each run

---

## Observability & Logging

### Logging Strategy

**Log Every State Change** (as per requirements)

#### Entry Points
```
[INFO] Request received: source=5, destination=8, requestId=abc
[INFO] Status query received
```

#### Request Processing
```
[INFO] Request created: ID=abc, Journey=5→8
[INFO] HallCall created: Floor=5, Direction=UP, HallCallId=xyz
[INFO] HallCall assigned: HallCallId=xyz → Elevator 2
[INFO] Request assigned to HallCall: RequestId=abc → HallCallId=xyz
```

#### Elevator Movement
```
[INFO] Tick 42 started
[INFO] Elevator 2: MOVE_UP from floor 5 to floor 6
[INFO] Elevator 2: Arrived at floor 6
[INFO] Elevator 2: OPEN_DOORS at floor 6
[INFO] Elevator 2: CLOSE_DOORS at floor 6
[INFO] Elevator 1: IDLE at floor 3
```

#### Hall Call Completion
```
[INFO] Elevator 2: HallCall completed at floor 6 UP
[INFO] Request fulfilled: RequestId=abc, WaitTime=5.2s
```

#### Periodic Status
```
[INFO] Tick 42 complete:
       E1[Floor=3, Dir=IDLE, State=IDLE, Destinations=[], HallCalls=0]
       E2[Floor=6, Dir=UP, State=LOADING, Destinations=[8,10], HallCalls=2]
       E3[Floor=9, Dir=DOWN, State=MOVING, Destinations=[5,2], HallCalls=1]
       E4[Floor=1, Dir=IDLE, State=IDLE, Destinations=[], HallCalls=0]
       Pending HallCalls: 3
```

#### Errors
```
[ERROR] Invalid request: source=5, destination=5 (same floor)
[ERROR] Request rejected: HallCallQueue full (18/18)
[WARN] No elevator available for HallCall: Floor=7, Direction=UP (will retry)
```

### Logging Locations

| Component | What to Log |
|-----------|-------------|
| **IElevatorRequestService** | Request received, created, assigned, errors |
| **Building** | Hall call created/assigned, tick started/completed, errors |
| **Elevator** | Movement, arrivals, door operations, hall call completion |
| **HallCallQueue** | Hall call created, capacity warnings |
| **Request** | Request created, fulfilled |
| **SystemOrchestrator** | System startup, shutdown, configuration loaded |

---

## System Initialization

### Startup Sequence (SystemOrchestrator.BootUp)

```
1. Load SimulationConfiguration from JSON
   ├── If file missing → use defaults
   ├── Validate configuration
   └── Log: "Configuration loaded: 10 floors, 4 elevators, 1s tick"

2. Create ILogger (ConsoleLogger)
   └── Log: "Logger initialized"

3. Create ITimeService
   ├── RealTimeService (production)
   └── FastTimeService (testing)

4. Create Domain Services
   ├── IScheduler (NearestElevatorScheduler)
   ├── IElevatorMovementCoordinator
   └── Log: "Domain services initialized"

5. Create Building (Aggregate Root)
   ├── Building creates 4 Elevators in constructor
   ├── Each elevator starts at floor 0, IDLE
   └── Log: "Building initialized: 4 elevators created"

6. Create IElevatorRequestService
   └── Inject Building, Logger

7. Create Application Services
   ├── ElevatorSimulationService (inject Building, Coordinator, TimeService)
   └── RandomRequestGenerator (inject RequestService, TimeService, Config)

8. Start Tasks
   ├── Task.Run(() => ElevatorSimulationService.RunAsync(cancellationToken))
   ├── Task.Run(() => RandomRequestGenerator.GenerateAsync(cancellationToken))
   └── Log: "Simulation started"

9. Return control to Program.cs
   └── Wait for Ctrl+C or shutdown signal
```

### Dependency Injection (Manual Wiring)

```csharp
// Phase 1: Manual DI (no framework)
var config = SimulationConfiguration.Load("config.json");
var logger = new ConsoleLogger();
var timeService = new RealTimeService();
var scheduler = new NearestElevatorScheduler();
var coordinator = new ElevatorMovementCoordinator();

var building = new Building(
    floorCount: config.FloorCount,
    elevatorCount: config.ElevatorCount,
    logger: logger
);

var requestService = new ElevatorRequestService(
    building: building,
    logger: logger
);

var simulationService = new ElevatorSimulationService(
    building: building,
    coordinator: coordinator,
    timeService: timeService,
    logger: logger,
    tickInterval: config.TickInterval
);

var requestGenerator = new RandomRequestGenerator(
    requestService: requestService,
    timeService: timeService,
    config: config,
    logger: logger
);

var orchestrator = new SystemOrchestrator(
    simulationService: simulationService,
    requestGenerator: requestGenerator,
    logger: logger
);

await orchestrator.BootUp();
```

---

## Graceful Shutdown

### Shutdown Sequence (SystemOrchestrator.Shutdown)

```
1. User presses Ctrl+C
   └── CancellationTokenSource.Cancel()

2. ElevatorSimulationService.RunAsync() receives cancellation
   ├── Exit loop
   ├── Log: "Simulation service stopping..."
   └── Complete current tick (don't interrupt)

3. RandomRequestGenerator.GenerateAsync() receives cancellation
   ├── Exit loop
   └── Log: "Request generator stopping..."

4. Wait for all tasks to complete (with timeout)
   ├── Task.WaitAll(tasks, timeout: 5 seconds)
   └── Log: "All tasks stopped"

5. Log final statistics
   ├── Total requests processed
   ├── Total hall calls completed
   ├── Average wait time
   └── Log: "Shutdown complete"

6. Exit application
```

---

## Configuration Management

### SimulationConfiguration Structure

```json
{
  "building": {
    "floorCount": 10,
    "elevatorCount": 4
  },
  "timing": {
    "simulationTickMs": 1000,
    "requestFrequencyMs": 5000
  },
  "requestGeneration": {
    "minFloor": 1,
    "maxFloor": 10
  }
}
```

### Defaults (if config.json missing)

```csharp
public static SimulationConfiguration Default => new()
{
    FloorCount = 10,
    ElevatorCount = 4,
    SimulationTickMs = 1000,
    RequestFrequencyMs = 5000,
    MinFloor = 1,
    MaxFloor = 10
};
```

### Validation Rules

```
- FloorCount: 2 to 100
- ElevatorCount: 1 to 10
- SimulationTickMs: 100 to 10000
- RequestFrequencyMs: 1000 to 60000
- MinFloor < MaxFloor
```

**Fail-fast:** Throw exception on invalid configuration at startup.

---

## Concurrency Model

### Single Global Lock Strategy

**Lock Owner:** Building (aggregate root)

**Lock Scope:**
- ProcessRequest() - entire operation
- ProcessTick() - entire operation
- GetAllElevatorStatus() - entire operation

**Task Model:**
- 1 task: ElevatorSimulationService (calls ProcessTick in loop)
- 1 task: RandomRequestGenerator (calls ProcessRequestAsync in loop)
- Total: 2 concurrent tasks competing for Building lock

**Why Not 4 Tasks (one per elevator)?**
- Simpler: Single atomic tick
- Deterministic: Fixed processing order
- Consistent: All elevators see same state
- Sufficient: 4 elevators don't need parallel processing

---

## Error Handling Strategy

### Validation Errors (Domain)

```
Building.ProcessRequest(request)
{
    if (request.Source < 1 || request.Source > MaxFloor)
        throw new ArgumentException("Invalid source floor");
    
    if (request.Destination < 1 || request.Destination > MaxFloor)
        throw new ArgumentException("Invalid destination floor");
    
    // ... continue processing
}
```

### Service Layer (Catch & Log)

```
IElevatorRequestService.ProcessRequestAsync(source, dest)
{
    try
    {
        var request = new Request(source, dest);
        building.ProcessRequest(request);
        logger.LogInfo($"Request processed: {request.Id}");
        return Result.Success();
    }
    catch (ArgumentException ex)
    {
        logger.LogError($"Invalid request: {ex.Message}");
        return Result.Failure(ex.Message);
    }
    catch (Exception ex)
    {
        logger.LogError($"Unexpected error: {ex}");
        return Result.Failure("Internal error");
    }
}
```

### Capacity Errors

```
HallCallQueue.GetOrCreate(floor, direction)
{
    if (Count >= 18)
    {
        logger.LogWarning("HallCallQueue full (18/18)");
        return null; // Caller must handle
    }
    // ... create hall call
}
```

### No Elevator Available (Not an Error)

```
IScheduler.SelectElevator(hallCall, elevators)
{
    // ... try to find elevator
    
    if (bestCandidate == null)
    {
        logger.LogInfo("No elevator available, hall call stays PENDING");
        return null; // Will retry on next tick
    }
    
    return bestCandidate;
}
```

---

## Summary

### Key Design Decisions

| Aspect | Decision | Rationale |
|--------|----------|-----------|
| **Architecture** | Clean Architecture (3 layers) | Clear separation, testable |
| **Communication** | Direct method calls | Simple, single-process |
| **Async/Sync** | Async API, sync domain | Non-blocking entry, consistent domain |
| **Concurrency** | Single global lock | Simple, deterministic, sufficient for 4 elevators |
| **Tick Processing** | Single ProcessTick, fixed order | Atomic tick, deterministic |
| **Scheduling** | Direction-aware | Efficient, supports 1-to-many hall calls |
| **State** | In-memory only | No persistence needed for Phase 1 |
| **Logging** | Every state change | Full observability |
| **Configuration** | JSON with defaults | Flexible, fail-fast validation |
| **Caching** | None | Lock provides consistency |

### Data Flows

1. **Request Flow:** Generator → RequestService → Building → Scheduler → Elevator
2. **Tick Flow:** SimulationService → Building → Coordinator → Elevators (sequential)
3. **Query Flow:** Console → RequestService → Building → ElevatorStatus (snapshot)

### Component Interactions

- **Application Layer** orchestrates timing and lifecycle
- **Domain Layer** owns all business logic and state
- **Infrastructure Layer** provides technical capabilities (logging, time, config)

### Next Steps

With high-level design complete, we can proceed to:
- **Phase 6**: Data Design (detailed data structures, state machines)
- **Phase 7**: APIs & Contracts (interface signatures, contracts)
- **Phase 8**: Failure Modes (error scenarios, mitigation)
