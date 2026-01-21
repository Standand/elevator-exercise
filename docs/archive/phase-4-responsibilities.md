# Phase 4 - Responsibilities

## Overview

This phase defines what each component **owns** and **does NOT own**, applying Single Responsibility Principle (SRP) at the system level.

**Key Principle:** If something "knows too much," the design will break.

---

## 1. Building (Aggregate Root)

### ✅ Owns (Responsibilities)

**State Ownership:**
- Collection of elevators (4 instances)
- HallCallQueue (pending hall calls)
- Collection of all requests
- Building configuration (floors, elevator count)

**Coordination:**
- Request-to-HallCall assignment
- HallCall-to-Elevator assignment (via scheduler)
- Aggregate consistency boundary enforcement

**Operations:**
- Receive new requests
- Create or merge hall calls
- Assign hall calls to elevators
- Query elevator statuses
- Remove completed hall calls

**Invariants:**
- Ensures no duplicate hall calls
- Ensures max 18 concurrent hall calls
- Ensures each hall call assigned to at most one elevator
- Ensures all elevators belong to this building

**Entity Creation:**
- Creates all Elevator instances in constructor
- Passes elevator ID and initial floor to each Elevator
- Elevators created once at initialization

**Concurrency:**
- Owns global lock (aggregate root responsibility)
- All state-modifying operations acquire lock
- Provides strong consistency guarantee

**Error Handling:**
- Validates floor range (1 to maxFloor)
- Throws ArgumentException for invalid floors
- Logs all errors via ILogger
- Ensures transactional consistency (no partial updates)

**Event Publishing:**
- Publishes HallCallAssigned after assignment

### ❌ Does NOT Own

**Movement Logic:**
- Does NOT move elevators (Elevator's responsibility)
- Does NOT determine next elevator action (IElevatorMovementCoordinator's responsibility)

**Scheduling Logic:**
- Does NOT decide which elevator is optimal (IScheduler's responsibility)
- Does NOT implement scheduling algorithms

**Timing:**
- Does NOT manage time delays (ITimeService's responsibility)
- Does NOT orchestrate timing (ElevatorSimulationService's responsibility)

**Request Generation:**
- Does NOT generate random requests (RandomRequestGenerator's responsibility)

**Validation:**
- Does NOT validate floor numbers (Floor value object's responsibility)
- Does NOT validate destinations (DestinationSet's responsibility)

### 🔒 Consistency Boundary

Building is the **aggregate root** - all modifications to elevators and hall calls go through Building to maintain invariants.

---

## 2. Elevator (Entity)

### ✅ Owns (Responsibilities)

**State Ownership:**
- Current floor position
- Current direction (UP, DOWN, IDLE)
- Current state (IDLE, MOVING, STOPPED, LOADING)
- Destination queue (floors to visit)
- List of assigned hall calls

**Movement:**
- Move up one floor
- Move down one floor
- Stop at floor
- Load passengers (state transition)

**State Transitions:**
- IDLE → MOVING (when destinations added)
- MOVING → STOPPED (when reaches floor)
- STOPPED → LOADING (doors open)
- LOADING → MOVING or IDLE (based on remaining destinations)

**Hall Call Acceptance:**
- Determine if can accept new hall call
- Check direction compatibility
- Check floor range compatibility

**Operations:**
- Add destination to queue
- Remove destination from queue
- Assign hall call
- Complete hall call
- Query current status

**Invariants:**
- CurrentFloor in valid range [0, maxFloors-1]
- Direction matches next destination
- Cannot be MOVING without destinations
- All assigned hall calls in same direction
- All assigned hall calls between current floor and longest destination

**Error Handling:**
- Validates destinations before adding to queue
- Logs all state transitions via ILogger
- Does NOT throw exceptions (defensive programming)
- Returns success/failure status

**Event Publishing:**
- Publishes ElevatorMoved after each floor change
- Publishes ElevatorDoorOpened when state → LOADING
- Publishes ElevatorDoorClosed when state → MOVING/IDLE
- Publishes HallCallCompleted when hall call fulfilled

**Hall Call Management:**
- Can accept MULTIPLE hall calls (1-to-many relationship)
- All hall calls must be in same direction
- Hall calls picked up between current floor and furthest destination
- Completes hall call when elevator leaves that floor in that direction

### ❌ Does NOT Own

**Assignment Decision:**
- Does NOT decide if it should be assigned to hall call (IScheduler's responsibility)
- Does NOT select itself for requests

**Timing:**
- Does NOT manage movement delays (ElevatorSimulationService's responsibility)
- Does NOT know how long to wait

**Other Elevators:**
- Does NOT know about other elevators
- Does NOT coordinate with other elevators

**Hall Call Management:**
- Does NOT create hall calls (Building's responsibility)
- Does NOT manage hall call queue

**Request Management:**
- Does NOT receive requests directly (Building's responsibility)
- Does NOT track individual passenger requests

### 🎯 Single Reason to Change

Elevator behavior or state machine logic changes.

---

## 3. HallCall (Entity)

### ✅ Owns (Responsibilities)

**State Ownership:**
- Floor where hall button pressed
- Direction (UP or DOWN)
- DestinationSet (accumulated destinations)
- Status (PENDING, ASSIGNED, COMPLETED)
- Assigned elevator ID
- Creation timestamp

**Destination Management:**
- Add new destination (via DestinationSet)
- Get all destinations
- Check if empty

**Status Management:**
- Mark as assigned (when elevator assigned)
- Mark as completed (when passengers picked up)

**Invariants:**
- Floor in valid range
- Direction must be UP or DOWN (not IDLE)
- Floor 0 → must be UP
- Floor maxFloors-1 → must be DOWN
- Destinations non-empty
- All destinations match direction
- Status ASSIGNED → AssignedElevatorId not null

**Error Handling:**
- Validates direction/floor compatibility in constructor
- Throws ArgumentException for invalid state
- DestinationSet handles destination validation

**Lifecycle:**
- Once ASSIGNED, only that elevator can complete it
- Once COMPLETED, cannot be reassigned
- Max 2 hall calls per floor (one UP, one DOWN)
- When elevator leaves floor in that direction, hall call is COMPLETED

### ❌ Does NOT Own

**Assignment Decision:**
- Does NOT decide which elevator should service it (IScheduler's responsibility)
- Does NOT assign itself to elevator

**Deduplication:**
- Does NOT manage deduplication logic (HallCallQueue's responsibility)

**Validation:**
- Does NOT validate destinations (DestinationSet's responsibility)

**Elevator Coordination:**
- Does NOT know about elevator state
- Does NOT track elevator arrival

**Request Tracking:**
- Does NOT track individual requests (Request's responsibility)
- Does NOT know how many passengers

### 🎯 Single Reason to Change

Hall call lifecycle or status management changes.

---

## 4. Request (Entity)

### ✅ Owns (Responsibilities)

**State Ownership:**
- Unique request ID
- Journey (source, destination, direction)
- Request timestamp
- Status (CREATED, ASSIGNED_TO_HALLCALL, COMPLETED)
- Assigned hall call ID

**Lifecycle Management:**
- Mark as assigned to hall call
- Mark as completed
- Track status transitions

**Metrics:**
- Calculate wait time (completion time - request time)
- Provide journey details for analytics

**Invariants:**
- ID is unique
- Journey is valid (source ≠ destination)
- Status ASSIGNED_TO_HALLCALL → AssignedHallCallId not null
- RequestTime cannot be in future

**Entity Creation:**
- Created by IElevatorRequestService.ProcessRequestAsync()
- Receives unique ID during creation

**Error Handling:**
- Validates Journey in constructor
- Throws ArgumentException for source == destination

**Event Publishing:**
- Publishes RequestCreated when instantiated
- Publishes RequestFulfilled when status → COMPLETED

### ❌ Does NOT Own

**Hall Call Creation:**
- Does NOT create hall calls (Building's responsibility)

**Assignment Decision:**
- Does NOT decide which hall call to join (Building's responsibility)

**Elevator Selection:**
- Does NOT know about elevators
- Does NOT track which elevator is coming

**Journey Execution:**
- Does NOT move elevator (Elevator's responsibility)

### 🎯 Single Reason to Change

Request tracking or metrics requirements change.

---

## 5. Journey (Value Object)

### ✅ Owns (Responsibilities)

**Data:**
- Source floor
- Destination floor
- Derived direction
- Request timestamp

**Calculations:**
- Calculate wait time
- Calculate distance
- Derive direction from floors

**Validation:**
- Ensure source ≠ destination
- Ensure floors are valid

**Invariants:**
- Immutable after creation
- Direction always matches source/destination relationship

### ❌ Does NOT Own

**Execution:**
- Does NOT execute the journey
- Does NOT track journey progress

**Assignment:**
- Does NOT assign to hall call
- Does NOT assign to elevator

### 🎯 Single Reason to Change

Journey calculation logic changes.

---

## 6. DestinationSet (Value Object)

### ✅ Owns (Responsibilities)

**Data:**
- Set of destination floors
- Source floor (for validation)
- Direction (for validation)

**Validation:**
- Ensure destination ≠ source
- Ensure UP → destination > source
- Ensure DOWN → destination < source
- Prevent duplicates (HashSet)

**Operations:**
- Add destination (with validation)
- Get all destinations
- Check if empty
- Get count

**Invariants:**
- All destinations match direction
- No destination equals source
- Non-empty (at least 1 destination)

### ❌ Does NOT Own

**Hall Call Management:**
- Does NOT manage hall call lifecycle
- Does NOT track assignment

**Elevator Coordination:**
- Does NOT know about elevators

### 🎯 Single Reason to Change

Destination validation rules change.

---

## 7. HallCallQueue (Value Object)

### ✅ Owns (Responsibilities)

**Data:**
- Dictionary of hall calls keyed by (Floor, Direction)

**Deduplication:**
- GetOrCreate hall call (idempotent)
- Ensure only one hall call per (floor, direction)

**Operations:**
- Add or merge hall call
- Remove hall call
- Get pending hall calls
- Check if full (18 max)
- Get count

**Invariants:**
- No duplicate keys
- Max 18 hall calls
- All hall calls have unique (floor, direction)

**Entity Creation:**
- Creates HallCall instances in GetOrCreate()
- Returns existing if duplicate
- Passes floor, direction, timestamp

**Error Handling:**
- Returns null if queue is full (18 max)
- Caller must handle capacity check

**Event Publishing:**
- Publishes HallCallCreated when new hall call created

### ❌ Does NOT Own

**Hall Call Creation:**
- Does NOT initialize hall call state beyond constructor
- Caller provides initial destinations

**Assignment:**
- Does NOT assign hall calls to elevators (Building's responsibility)

**Scheduling:**
- Does NOT decide which hall call to process next (IScheduler's responsibility)

### 🎯 Single Reason to Change

Deduplication logic or capacity rules change.

---

## 8. ElevatorStatus (Value Object)

### ✅ Owns (Responsibilities)

**Data:**
- Snapshot of elevator state at point in time
- Elevator ID, floor, direction, state
- Pending stops
- Assigned hall calls
- Has passengers flag

**Creation:**
- Factory method from Elevator entity
- Immutable snapshot

**Query Model:**
- Read-only representation for UI/API

**Invariants:**
- Immutable after creation
- Consistent snapshot (all data from same point in time)

### ❌ Does NOT Own

**Elevator State:**
- Does NOT modify elevator (read-only)
- Does NOT maintain live state

**Updates:**
- Does NOT update itself (create new snapshot instead)

### 🎯 Single Reason to Change

Query model requirements change.

---

## 9. IScheduler (Domain Service)

### ✅ Owns (Responsibilities)

**Algorithm:**
- Select optimal elevator for hall call
- Consider elevator position, direction, load
- Pure logic (no side effects)

**Decision Making:**
- Which elevator is best for this hall call?
- Based on current state of all elevators

**Implementations:**
- NearestElevatorScheduler (simple)
- ScanScheduler (future - elevator algorithm)

**Invariants:**
- Same inputs → same output (deterministic)
- No side effects
- Stateless

**Error Handling:**
- Returns null if no suitable elevator found
- Caller must handle null case
- Never throws exceptions

### ❌ Does NOT Own

**Assignment Execution:**
- Does NOT assign hall call to elevator (Building's responsibility)
- Does NOT modify elevator state

**Elevator State:**
- Does NOT own elevator state
- Receives state as input

**Hall Call State:**
- Does NOT modify hall call
- Receives hall call as input

**Timing:**
- Does NOT consider timing/delays
- Pure spatial/state logic only

### 🎯 Single Reason to Change

Scheduling algorithm changes.

---

## 10. IElevatorRequestService (Domain Service)

### ✅ Owns (Responsibilities)

**Request Handling:**
- Receive elevator requests (source, destination)
- Create Request entity
- Coordinate with Building

**Query Operations:**
- Get all elevator statuses
- Get specific elevator status

**Orchestration:**
- Request → Journey → HallCall flow
- Coordinate Building operations

**Entity Creation:**
- Creates Request entity with unique ID
- Generates unique request IDs (GUID/UUID)

**Error Handling:**
- Validates source and destination floors
- Catches exceptions from Building
- Logs errors via ILogger
- Returns failure result to caller
- Does NOT throw exceptions (returns Result<T> pattern)

### ❌ Does NOT Own

**Building State:**
- Does NOT own elevators or hall calls
- Delegates to Building

**Scheduling:**
- Does NOT implement scheduling logic (IScheduler's responsibility)

**Timing:**
- Does NOT manage delays

**Movement:**
- Does NOT move elevators

### 🎯 Single Reason to Change

Request handling API changes.

---

## 11. ITimeService (Domain Service)

### ✅ Owns (Responsibilities)

**Time Abstraction:**
- Provide delay mechanism
- Provide current time
- Abstract away Task.Delay

**Implementations:**
- RealTimeService (production - actual delays)
- FastTimeService (testing - 100x faster)
- FakeTimeService (unit tests - instant)

**Testability:**
- Enable fast tests
- Enable deterministic tests

### ❌ Does NOT Own

**Timing Configuration:**
- Does NOT know how long to delay (caller's responsibility)
- Does NOT know movement vs loading time

**Simulation Control:**
- Does NOT control simulation start/stop

**Domain Logic:**
- Does NOT decide when to delay
- Pure infrastructure concern

### 🎯 Single Reason to Change

Time abstraction mechanism changes.

---

## 12. IElevatorMovementCoordinator (Domain Service)

### ✅ Owns (Responsibilities)

**Decision Logic:**
- Determine next action for elevator (WHAT)
- MOVE_UP, MOVE_DOWN, STOP, LOAD, IDLE
- Pure domain logic

**State Analysis:**
- Analyze elevator state
- Determine next step in journey
- Consider destinations and hall calls

**Command Generation:**
- Return MovementCommand
- Include action and target floor

**Invariants:**
- Stateless
- No side effects
- Deterministic

### ❌ Does NOT Own

**Execution:**
- Does NOT execute movement (ElevatorSimulationService's responsibility)
- Does NOT manage timing

**Elevator State:**
- Does NOT modify elevator
- Receives state as input

**Timing:**
- Does NOT know how long actions take
- Does NOT delay

### 🎯 Single Reason to Change

Movement decision logic changes.

---

## 13. ElevatorSimulationService (Application Service)

### ✅ Owns (Responsibilities)

**Orchestration:**
- Execute elevator movements with timing
- Coordinate WHEN and HOW (not WHAT)

**Timing:**
- Apply movement delays
- Apply loading delays
- Use ITimeService

**Execution Loop:**
- Run elevator continuously
- Get next command from coordinator
- Execute with appropriate delay
- Handle cancellation

**Configuration:**
- Movement time (ms)
- Loading time (ms)
- Stop time (ms)

### ❌ Does NOT Own

**Domain Logic:**
- Does NOT decide what elevator should do (IElevatorMovementCoordinator's responsibility)

**Elevator State:**
- Does NOT own elevator state
- Delegates to Elevator entity

**Scheduling:**
- Does NOT schedule hall calls

**Request Handling:**
- Does NOT receive requests

### 🎯 Single Reason to Change

Simulation orchestration or timing strategy changes.

---

## 14. RandomRequestGenerator (Application Service)

### ✅ Owns (Responsibilities)

**Request Generation:**
- Generate random source floors
- Generate random destination floors
- Ensure source ≠ destination

**Timing:**
- Generate at configured frequency
- Use ITimeService for delays

**Lifecycle:**
- Start generation
- Stop generation
- Run continuously until cancelled

**Configuration:**
- Request frequency (ms)
- Floor range (min, max)

**Error Handling:**
- Log generation errors
- Continue on individual failures

### ❌ Does NOT Own

**Request Handling:**
- Does NOT handle requests (IElevatorRequestService's responsibility)

**Building State:**
- Does NOT know about elevators or hall calls

**Validation:**
- Does NOT validate beyond basic rules
- Delegates to domain

### 🎯 Single Reason to Change

Request generation strategy changes.

---

## 15. ILogger (Infrastructure Interface)

### ✅ Owns (Responsibilities)

**Console Logging:**
- Log to console output
- Format log messages

**Log Levels:**
- Info (normal operations)
- Warning (recoverable issues)
- Error (failures)
- Debug (detailed diagnostics)

**Structured Logging:**
- Timestamp
- Component/context
- Message
- Optional data

### ❌ Does NOT Own

**Business Logic:**
- Does NOT contain domain logic

**State Management:**
- Does NOT store logs (console only)

**Filtering:**
- Does NOT filter logs (logs everything)

### 🎯 Single Reason to Change

Logging implementation or format changes.

---

## 16. SimulationConfiguration (Value Object)

### ✅ Owns (Responsibilities)

**Configuration Data:**
- Building configuration (floors, elevator count)
- Timing configuration (simulation tick, request frequency)
- Defaults when file missing

**Validation:**
- Validate floor range (min < max)
- Validate elevator count (> 0)
- Validate timing values (> 0)
- Fail fast on invalid config

**Loading:**
- Load from JSON file
- Parse and deserialize
- Apply defaults

**Immutability:**
- Read-only after construction
- Thread-safe access

**Error Handling:**
- Throw on invalid configuration
- Clear error messages

### ❌ Does NOT Own

**Domain Logic:**
- Does NOT validate domain rules
- Does NOT know about Building/Elevator state

**File I/O:**
- Does NOT manage file paths (injected)

**Runtime Changes:**
- Does NOT support hot-reload

### 🎯 Single Reason to Change

Configuration structure or validation rules change.

---

## 17. SystemOrchestrator (Application Service)

### ✅ Owns (Responsibilities)

**System Initialization:**
- Load configuration
- Create Building instance
- Create ElevatorSimulationService
- Create RandomRequestGenerator
- Wire up dependencies

**Lifecycle Management:**
- Start simulation tasks
- Start request generator
- Manage CancellationToken

**Graceful Shutdown:**
- Cancel all tasks
- Wait for task completion (with timeout)
- Log shutdown progress
- Ensure clean state

**Error Handling:**
- Catch unhandled exceptions
- Log startup/shutdown errors
- Ensure proper cleanup

**Entry Point:**
- Called by Program.cs
- BootUp() method
- Shutdown() method

### ❌ Does NOT Own

**Domain Logic:**
- Does NOT contain business rules

**Simulation Logic:**
- Does NOT orchestrate ticks (ElevatorSimulationService's responsibility)

**Configuration Creation:**
- Does NOT validate config (SimulationConfiguration's responsibility)

### 🎯 Single Reason to Change

System startup/shutdown orchestration changes.

---

## 18. Domain Events (Cross-Cutting)

### ✅ Owns (Responsibilities)

**Event Publishing:**
- Each entity publishes its own events
- Direct logging (no event broker)

**Event Types (from Phase 3):**
- ElevatorMoved (Elevator publishes)
- ElevatorDoorOpened (Elevator publishes)
- ElevatorDoorClosed (Elevator publishes)
- HallCallCreated (HallCallQueue publishes)
- HallCallAssigned (Building publishes)
- HallCallCompleted (Elevator publishes)
- RequestCreated (Request publishes)
- RequestFulfilled (Request publishes)

**Logging:**
- Log event immediately when published
- Include timestamp, entity ID, event data

### ❌ Does NOT Own

**Event Storage:**
- Does NOT persist events (console logging only)

**Event Replay:**
- Does NOT support event sourcing

**Async Processing:**
- Does NOT use event broker/queue

### 🎯 Single Reason to Change

Event structure or logging strategy changes.

**Note:** Events are for observability only in Phase 1. No event-driven architecture.

---

## Responsibility Matrix

| Component | Owns | Does NOT Own |
|-----------|------|--------------|
| **Building** | Elevators, HallCallQueue, Requests, Coordination, Lock | Movement, Scheduling, Timing, Generation |
| **Elevator** | Position, State, Movement, Destinations, Event publishing | Assignment decision, Timing, Other elevators |
| **HallCall** | Floor, Direction, Destinations, Status | Assignment decision, Deduplication, Validation |
| **Request** | ID, Journey, Status, Lifecycle, Event publishing | Hall call creation, Assignment, Execution |
| **Journey** | Source, Destination, Calculations | Execution, Assignment |
| **DestinationSet** | Destinations, Validation | Hall call management, Assignment |
| **HallCallQueue** | Deduplication, Capacity, Event publishing | Hall call creation, Assignment, Scheduling |
| **ElevatorStatus** | Snapshot data | Live state, Updates |
| **IScheduler** | Algorithm, Selection logic | Assignment execution, State ownership |
| **IElevatorRequestService** | Request API, Orchestration | Building state, Scheduling, Timing |
| **ITimeService** | Time abstraction | Timing configuration, Domain logic |
| **IElevatorMovementCoordinator** | WHAT to do (logic) | HOW/WHEN to do (execution, timing) |
| **ElevatorSimulationService** | HOW/WHEN (orchestration, timing) | WHAT to do (domain logic) |
| **RandomRequestGenerator** | Generation logic, Frequency | Request handling, Building state |
| **ILogger** | Console logging, Formatting | Business logic, Log storage |
| **SimulationConfiguration** | Config loading, Validation, Defaults | Domain validation, Runtime changes |
| **SystemOrchestrator** | Initialization, Shutdown, Lifecycle | Domain logic, Simulation orchestration |
| **Domain Events** | Event publishing, Logging | Event storage, Async processing |

---

## Concurrency & Transaction Responsibilities

### 🔒 Lock Ownership

**Building (Aggregate Root):**
- Owns a single global lock (`lock` statement in C# / `synchronized` in Java)
- All state-modifying operations acquire this lock
- Ensures strong consistency across the aggregate

### 🔐 Operations Requiring Lock

**Write Operations (require lock):**
- `ProcessRequest()` - creates HallCall, assigns elevator
- `ProcessTick()` - moves elevators, completes hall calls
- `GetAllElevatorStatus()` - reads consistent snapshot

**Read Operations (require lock for consistency):**
- Any operation reading multiple elevators
- Any operation reading HallCallQueue + Elevator state

### ⚡ Tradeoffs

#### Option A: Single Global Lock (Chosen for Phase 1)

**Pros:**
- ✅ Simple to implement and reason about
- ✅ No deadlocks (single lock)
- ✅ Strong consistency guaranteed
- ✅ Sufficient for 4 elevators

**Cons:**
- ❌ Lower concurrency (serialized access)
- ❌ Potential bottleneck at scale

**When to Use:**
- Small number of elevators (< 10)
- Simple domain model
- Correctness > throughput

---

#### Option B: Fine-Grained Locks (Future Enhancement)

**Pros:**
- ✅ Higher concurrency
- ✅ Better scalability

**Cons:**
- ❌ Deadlock risk
- ❌ Complex lock ordering
- ❌ Harder to maintain invariants

**When to Use:**
- Large number of elevators (> 10)
- Throughput critical

---

#### Option C: Lock-Free (Concurrent Collections)

**Pros:**
- ✅ Maximum concurrency
- ✅ No blocking

**Cons:**
- ❌ Very complex
- ❌ Consistency harder to guarantee
- ❌ Requires careful design

**When to Use:**
- Extreme scale
- Expert team

---

### 📋 Transaction Boundaries

**Transaction = Operations within a single lock acquisition**

**Transaction 1: ProcessRequest**
```
lock (Building)
{
  1. Validate request
  2. Check duplicate (HallCallQueue)
  3. Create/update HallCall
  4. Select elevator (IScheduler)
  5. Assign to elevator
  6. Publish events
}
```

**Transaction 2: ProcessTick**
```
lock (Building)
{
  For each elevator:
    1. Decide action (IElevatorMovementCoordinator)
    2. Execute action (move/open/close/idle)
    3. Update state
    4. Complete hall calls if arrived
    5. Publish events
}
```

**Transaction 3: GetAllElevatorStatus**
```
lock (Building)
{
  1. Read all elevator states
  2. Create consistent snapshot
}
```

### 🔄 Concurrency Model

**Async/Await Usage:**
- `IElevatorRequestService.ProcessRequestAsync()` - async API
- `ElevatorSimulationService.RunAsync()` - async execution
- `ITimeService.DelayAsync()` - async delays

**Task Management:**
- One task per elevator (4 tasks total)
- One task for request generator
- All tasks use `CancellationToken` for shutdown

**Thread Safety:**
- Building: Thread-safe (uses lock)
- Elevator: NOT thread-safe (accessed only via Building lock)
- HallCall: NOT thread-safe (accessed only via Building lock)
- Request: NOT thread-safe (accessed only via Building lock)
- Value Objects: Immutable (inherently thread-safe)

### 🚨 Responsibility Assignment

| Component | Concurrency Responsibility |
|-----------|---------------------------|
| **Building** | Owns lock, ensures thread safety |
| **Elevator** | NOT thread-safe, relies on Building lock |
| **HallCall** | NOT thread-safe, relies on Building lock |
| **Request** | NOT thread-safe, relies on Building lock |
| **HallCallQueue** | NOT thread-safe, relies on Building lock |
| **DestinationSet** | Immutable (thread-safe) |
| **Journey** | Immutable (thread-safe) |
| **ElevatorSimulationService** | Manages tasks, uses await |
| **RandomRequestGenerator** | Single task, uses await |
| **SystemOrchestrator** | Manages CancellationToken |

---

## Dependency Rules

### Layer Dependencies (Clean Architecture)

```
Application Layer
  ├── ElevatorSimulationService
  ├── RandomRequestGenerator
  ├── SystemOrchestrator
  └── depends on ↓

Domain Layer
  ├── Entities (Building, Elevator, HallCall, Request)
  ├── Value Objects (Journey, DestinationSet, etc.)
  ├── Domain Services (IScheduler, ITimeService, ILogger, etc.)
  └── depends on ↓

Infrastructure Layer
  ├── RealTimeService (implements ITimeService)
  ├── FastTimeService (implements ITimeService)
  ├── ConsoleLogger (implements ILogger)
  ├── SimulationConfiguration
  └── Configuration file I/O
```

**Rule:** Dependencies point inward. Domain never depends on Application or Infrastructure.

---

## Coupling Points

### Acceptable Coupling

✅ Building → Elevator (composition)
✅ Building → HallCallQueue (composition)
✅ HallCall → DestinationSet (composition)
✅ Request → Journey (composition)
✅ ElevatorSimulationService → IElevatorMovementCoordinator (dependency injection)
✅ ElevatorSimulationService → ITimeService (dependency injection)

### Unacceptable Coupling

❌ Elevator → Building (would create circular dependency)
❌ HallCall → Elevator (should be one-way association)
❌ Domain → Application (violates clean architecture)
❌ IScheduler → ElevatorSimulationService (domain shouldn't know application)

---

## Cohesion Boundaries

### High Cohesion (Good)

✅ **Elevator** - All movement and state logic together
✅ **DestinationSet** - All destination validation together
✅ **HallCallQueue** - All deduplication logic together
✅ **Journey** - All journey calculations together

### What Would Break Cohesion (Bad)

❌ Putting scheduling logic in Elevator
❌ Putting timing in IScheduler
❌ Putting validation in Building
❌ Putting movement in Building

---

## Summary

Each component has a **single, clear responsibility**:

### Entities
- **Building**: Aggregate root - owns lock, coordinates operations, creates Elevators
- **Elevator**: Manages state/movement, accepts multiple hall calls (1-to-many)
- **HallCall**: Tracks hall button press, accumulates destinations
- **Request**: Tracks individual passenger journey, provides metrics

### Value Objects
- **Journey**: Immutable source/destination pair
- **DestinationSet**: Validates and manages destinations
- **HallCallQueue**: Deduplicates hall calls, creates HallCall entities
- **ElevatorStatus**: Immutable snapshot

### Domain Services
- **IScheduler**: Pure algorithm - selects optimal elevator (stateless)
- **IElevatorRequestService**: Creates Request entities, orchestrates Building operations
- **ITimeService**: Abstracts time for testability
- **IElevatorMovementCoordinator**: Pure logic - decides elevator actions (stateless)

### Application Services
- **ElevatorSimulationService**: Orchestrates execution (HOW/WHEN)
- **RandomRequestGenerator**: Generates test requests
- **SystemOrchestrator**: Manages system lifecycle (startup/shutdown)

### Infrastructure
- **ILogger**: Console logging for observability
- **SimulationConfiguration**: Loads/validates configuration, provides defaults

### Cross-Cutting
- **Domain Events**: Published by entities, logged immediately (no broker)

### Key Design Decisions
1. **Concurrency**: Single global lock (aggregate root)
2. **Entity Creation**: Building → Elevators, IElevatorRequestService → Requests, HallCallQueue → HallCalls
3. **Error Handling**: Building validates and throws, services catch and log
4. **Hall Call Model**: Max 2 per floor (UP/DOWN), elevator can accept multiple
5. **Thread Safety**: Only Building is thread-safe, others rely on lock

**No component "knows too much."** Each has clear boundaries and dependencies flow in the correct direction.

---

## Next Steps

With responsibilities clearly defined, we can proceed to:
- **Phase 5**: High-Level Design (component interactions, data flow)
- **Phase 6**: Data Design (state management, data structures)
- **Phase 7**: APIs & Contracts (interfaces, methods)
