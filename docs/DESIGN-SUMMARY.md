# Elevator System Design Summary

## Documentation Status

✅ **Phase 0 - Problem Understanding** (Reviewed & Updated)
✅ **Phase 1 - Requirements** (Reviewed & Updated)  
✅ **Phase 2 - Constraints** (Complete)
✅ **Phase 3 - Domain Model** (Complete)

---

## Key Design Decisions

### 1. Request Model: Hall Call with Idempotency

**External Interface:**
```csharp
RequestElevator(int sourceFloor, int destinationFloor)
```

**Internal Representation:**
- Deduplicated by (floor, direction) tuple
- Multiple destinations accumulated in HashSet
- Max 18 unique hall calls possible

**Example:**
```
Request 1: Floor 5 → 8 (UP)   → HallCall(5, UP) destinations={8}
Request 2: Floor 5 → 10 (UP)  → HallCall(5, UP) destinations={8,10}
Request 3: Floor 5 → 8 (UP)   → HallCall(5, UP) destinations={8,10} (no change)
```

**Benefit:** Realistic behavior, bounded concurrency, prevents duplicate requests

---

### 2. Technology Stack

| Component | Technology | Version |
|-----------|------------|---------|
| Language | C# | Latest |
| Framework | .NET | 8 |
| Platform | Cross-platform | Windows, Linux, macOS |
| Concurrency | Task-based async/await | - |
| Collections | ConcurrentDictionary, ConcurrentQueue | - |
| Logging | ILogger, Console | - |
| Testing | xUnit or NUnit | - |
| Configuration | JSON | appsettings.json |

---

### 3. Architecture Pattern

**Hybrid: Stateful Domain + Stateless Services**

```
┌─────────────────────────────────────┐
│  Stateless Layer                    │
│  - ElevatorRequestService           │
│  - Scheduler (strategy)             │
│  - RequestGenerator                 │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│  Stateful Domain Layer              │
│  - Building (aggregate root)        │
│  - Elevator (entity)                │
│  - HallCall (entity)                │
└─────────────────────────────────────┘
```

---

### 4. Concurrency Model

**Threading:** Task-based async/await

```csharp
// Each elevator runs as async task
foreach (var elevator in elevators) 
{
    _ = elevator.RunAsync(cancellationToken);
}

// Elevator implementation
public async Task RunAsync(CancellationToken ct) 
{
    while (!ct.IsCancellationRequested) 
    {
        await ProcessNextActionAsync();
        await Task.Delay(movementTimeMs, ct);
    }
}
```

**Synchronization:** Global lock + concurrent collections

```csharp
private static readonly object _globalLock = new object();
private ConcurrentDictionary<(int, Direction), HallCall> _hallCalls;

lock (_globalLock) 
{
    // Critical section: assignment logic
}
```

---

### 5. Floor Numbering

**Dual representation for usability:**

| Context | Range | Example |
|---------|-------|---------|
| **Display (User)** | 1-10 | "Elevator at floor 5" |
| **Internal (Code)** | 0-9 | `elevatorPositions[4]` |

**Conversion:**
```csharp
int internalFloor = displayFloor - 1;  // 5 → 4
string display = (internalFloor + 1).ToString();  // 4 → "5"
```

---

### 6. Timing Configuration

**Configurable with test mode:**

```json
{
  "timing": {
    "movementTimeMs": 10000,
    "loadingTimeMs": 10000
  },
  "testMode": {
    "movementTimeMs": 100,
    "loadingTimeMs": 100
  }
}
```

**Speedup:** 100x faster testing (10s → 100ms)

---

### 7. Domain Model

**Core Entities:**
1. **Building** - Aggregate root, manages elevators and hall calls
2. **Elevator** - Has ID, position, state, destination queue
3. **HallCall** - Deduplicated by (floor, direction), accumulates destinations

**Value Objects:**
- Direction (UP, DOWN, IDLE)
- ElevatorState (IDLE, MOVING, STOPPED, LOADING)
- HallCallStatus (PENDING, ASSIGNED, COMPLETED)

**Domain Services:**
- IScheduler - Selects optimal elevator
- IElevatorRequestService - Handles requests

---

### 8. Key Invariants

**System Level:**
- Max 18 concurrent hall calls
- No duplicate hall calls for same (floor, direction)
- Every hall call assigned to exactly one elevator

**Elevator:**
- Floor always in valid range [0, maxFloors-1]
- Direction matches next destination relationship
- Moves one floor at a time

**HallCall:**
- Floor 0 (bottom) → must be UP
- Floor maxFloors-1 (top) → must be DOWN
- All destinations match direction (UP → dest > floor)

---

### 9. State Machines

**Elevator State Transitions:**
```
IDLE → MOVING → STOPPED → LOADING → IDLE
  ↑___________________________________|
```

**HallCall Lifecycle:**
```
PENDING → ASSIGNED → COMPLETED → Removed
```

---

### 10. Non-Functional Requirements

| Category | Requirement | Target |
|----------|-------------|--------|
| **Latency** | Request assignment | < 1s (P99) |
| **Latency** | Status query | < 100ms |
| **Throughput** | Request rate | 12 req/min (normal) |
| **Concurrent** | Max hall calls | 18 (max), 15-20 (typical) |
| **Consistency** | All operations | Strong consistency |
| **Availability** | Uptime | Continuous capable |
| **Recovery** | State on crash | Lost (acceptable) |
| **Testing** | Code coverage | 90% target |
| **Testing** | Unit test time | < 100ms each |
| **Testing** | Integration test | < 3s each |

---

## Project Structure

```
ElevatorSystem/                    (.NET 8 Console App)
  ├── Domain/                      (Entities, Value Objects)
  │   ├── Elevator.cs
  │   ├── HallCall.cs
  │   ├── Building.cs
  │   ├── Direction.cs
  │   └── ElevatorState.cs
  ├── Services/                    (Business Logic)
  │   ├── IElevatorRequestService.cs
  │   ├── ElevatorControlService.cs
  │   └── IScheduler.cs
  ├── Scheduling/                  (Algorithms)
  │   └── NearestElevatorScheduler.cs
  ├── Generator/                   (Request Generation)
  │   └── RandomRequestGenerator.cs
  ├── Configuration/               (Config)
  │   └── SimulationConfig.cs
  ├── Logging/                     (Output)
  │   └── ConsoleLogger.cs
  ├── Tests/                       (Unit & Integration)
  │   ├── ElevatorTests.cs
  │   ├── SchedulerTests.cs
  │   └── IntegrationTests.cs
  ├── appsettings.json            (Configuration)
  └── Program.cs                   (Entry Point)
```

---

## Configuration Example

```json
{
  "building": {
    "floors": 10,
    "numberOfElevators": 4,
    "initialFloor": 1
  },
  "timing": {
    "movementTimeMs": 10000,
    "loadingTimeMs": 10000,
    "testMode": {
      "movementTimeMs": 100,
      "loadingTimeMs": 100
    }
  },
  "requestGenerator": {
    "enabled": true,
    "frequencyMs": 5000,
    "minFloor": 1,
    "maxFloor": 10
  }
}
```

---

## Next Phases

- **Phase 4:** Responsibilities (SRP at component level)
- **Phase 5:** High-Level Design (component interactions)
- **Phase 6:** Data Design (state flow, data structures)
- **Phase 7:** APIs & Contracts (interfaces, methods)
- **Phase 8:** Failure Modes (error handling)
- **Phase 9:** Scalability (performance optimization)
- **Phase 10:** Low-Level Design (classes, patterns)
- **Phase 11:** Implementation (C# code)
- **Phase 12:** Testing Strategy (unit, integration tests)

---

## Design Principles Applied

✅ **Single Responsibility Principle** - Each entity has one clear purpose
✅ **Open/Closed Principle** - Scheduler strategy pattern allows extensions
✅ **Liskov Substitution** - Interface-based design
✅ **Interface Segregation** - Small, focused interfaces
✅ **Dependency Inversion** - Depend on abstractions (IScheduler)
✅ **Domain-Driven Design** - Rich domain model with invariants
✅ **KISS** - Keep it simple (no over-engineering)
✅ **YAGNI** - Only what's needed for Phase 1
