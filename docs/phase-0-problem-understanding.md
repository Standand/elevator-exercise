# Phase 0 - Problem Understanding

## System Classification

### System Type
- **Type**: Standalone stateful simulation application
- **Not**: Distributed system, web service, or production system
- **Reason**: Single building simulation with in-memory state, console output

### State Management
- **Stateful**: System maintains state between operations
- **State Storage**: In-memory (no external database)
- **State on Restart**: Lost on restart (acceptable for simulation)
- **State Components**:
  - Elevator positions (current floor)
  - Elevator directions (UP, DOWN, IDLE)
  - Elevator states (IDLE, MOVING, STOPPED, LOADING)
  - Pending hall calls (deduplicated by floor + direction)
  - Destination sets per hall call (accumulated destinations)
  - Destination queues per elevator (assigned stops)
  - Time/clock state for simulation

-> pending requests (request dispatcher)

### Consistency Model (To be revisited)
- **Strong Consistency**: Required (not eventual consistency)
- **Reason**: Elevator control needs immediate, accurate state visibility
- **Pattern**: REST API pattern (synchronous, not event-driven)
- **Not**: Event-driven with eventual consistency

### Correctness vs Latency
- **Correctness**: More important than latency
- **Correctness Rules**:
  1. No lost requests - Every request is assigned or queued
  2. No passengers left behind - Assigned requests must be fulfilled
  3. Directional consistency - Elevators don't reverse direction while carrying passengers
  4. No invalid states - Elevators can't be at invalid floors
  5. Atomic state updates - State changes are atomic (no intermediate states visible)

### System Scope
- **Internal System**: Simulation/demo, not public-facing
- **Extensibility**: Designed to support external users (REST API) in future
- **Current**: Console-based simulation
- **Future**: API layer can be added without changing core logic

## Architecture Decisions

### Request Interface Pattern
- **Current**: Method/interface pattern (direct method calls)
- **Future**: REST API handler can implement same interface
- **Design**: Interface abstraction allows both implementations
- **Example**:
  ```csharp
  public interface IElevatorRequestService 
  {
      void RequestElevator(int sourceFloor, int destinationFloor);
  }
  // Implementations:
  // - DirectElevatorService (current - method calls)
  // - RestElevatorService (future - REST API handler)
  ```

### Request Generator
- **Pattern**: Separate component in separate thread
- **Separation**: Generator doesn't know about controller
- **Controller**: Doesn't know about generator
- **Location**: Part of codebase but different responsibility
- **Threading**: Generator runs in its own thread

Notes: mahesh
1. elevator states

### Destination Input Strategy
- **Phase 1**: Simulated destinations (system generates random destination)
- **Future**: Support two-step input (hall call → destination input)
- **Design**: Interface designed to support both patterns
- **Reason**: Keep Phase 1 simple while remaining extensible

### Hall Call & Destination Tracking
- **Hall Call Deduplication**: By (floor, direction) tuple
- **Destination Accumulation**: Multiple destinations per hall call stored in HashSet
- **Elevator Stops**: Queue of floors where elevator must stop
- **Components**:
  - `HallCall`: Deduplicated by (floor, direction), contains set of destinations
  - `Elevator.Stops`: Queue of floors to visit
  - `Elevator.HasPassengers`: Boolean flag (derived from stops.Any())
- **Reason**: Realistic behavior, bounded concurrent requests (max 18)

-> there should be prioritization on the elevators as well.
-> consider ratelimiting is required for realistic usage rate and for the system to not break due to swelling request queues. ratelimiting protects the queues from swelling up.

### Capacity Management
- **Decision**: No capacity limit
- **Reason**: Problem says "weight limits out of scope", keep it simple
- **Future**: Can be added if needed (easier than weight calculations)

-> number of requests is also under capacity management. not just passengers.

### Timing Configuration
- **Storage**: JSON configuration file
- **Defaults**: Hardcoded constants (fallback if config missing)
- **Operations**: Different timings for different operations
  - Movement time (e.g., 10 seconds or 100ms in test)
  - Loading time (e.g., 10 seconds or 100ms in test)
- **Test Mode**: Configurable (e.g., 100ms for testing)

## Project Structure

```
ElevatorSystem/                    (.NET 8 Console Application)
  ├── Domain/                      (Core domain models)
  │   ├── Elevator.cs
  │   ├── HallCall.cs
  │   ├── Building.cs
  │   ├── Direction.cs (enum)
  │   └── ElevatorState.cs (enum)
  ├── Services/                    (Business logic)
  │   ├── IElevatorRequestService.cs
  │   ├── ElevatorControlService.cs
  │   └── IScheduler.cs
  ├── Scheduling/                  (Scheduling strategies)
  │   └── NearestElevatorScheduler.cs
  ├── Generator/                   (Request generation)
  │   └── RandomRequestGenerator.cs
  ├── Configuration/               (Config management)
  │   └── SimulationConfig.cs
  ├── Logging/                     (Console output)
  │   └── ConsoleLogger.cs
  └── Program.cs                   (Entry point)
```

## Key Assumptions

1. **Request Model**: Complete journey (source + destination) → Hall call (floor + direction) with destinations
2. **Idempotency**: Requests deduplicated by (floor, direction), destinations accumulated
3. **Initial State**: All elevators start at floor 1 (display), floor 0 (internal), IDLE state
4. **Request Generation**: Continuous random generation at 5-second intervals (configurable)
5. **Simulation Duration**: Runs until stopped (no fixed end, infinite generation)
6. **Output**: Console logging (simple, structured, no fancy UI)
7. **Threading**: Task-based async/await, all 4 elevators move concurrently
8. **Time Model**: Configurable (10s production, 100ms test mode)
9. **Technology**: .NET 8, C#, cross-platform

## Out of Scope (Explicitly)

- Weight limits
- Fire control
- Overrides
- Holds
- Capacity limits (for now)
- Multi-building support
- Persistence/recovery
- User authentication
- Request prioritization
- Use case of Elevator Getting stuck due to technical malfunction
- Energy Optimization

## Success Criteria

The system must demonstrate:
1. ✅ 4 elevators moving independently
2. ✅ Random requests being generated
3. ✅ Elevators responding to requests
4. ✅ Directional consistency (no yo-yo-ing)
5. ✅ Proper timing (configurable per operation)
6. ✅ Console output showing:
   - Current positions of all elevators
   - Requests being received
   - Elevators moving between floors
   - Elevators arriving at destinations
