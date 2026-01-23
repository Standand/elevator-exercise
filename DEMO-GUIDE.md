# Demo Guide

This guide explains how to demonstrate the Elevator Control System to reviewers or interviewers.

## Prerequisites

- .NET 8 SDK installed
- Terminal/PowerShell access
- 5-10 minutes for demo

## Demo Script

### Part 1: System Overview (2 minutes)

**What to say:**
> "This is a production-grade elevator control system implementing a complete 12-phase system design process. It manages 4 elevators across 10 floors using Clean Architecture and Domain-Driven Design principles."

**What to show:**
- Project structure in file explorer
- `docs/COMPLETE-DESIGN.md` (scroll quickly to show depth)
- `README.md` highlighting ADRs

### Part 2: Code Walkthrough (3 minutes)

**What to say:**
> "The implementation follows Clean Architecture with strict layer separation."

**Navigate through:**

1. **Domain Layer** (`src/ElevatorSystem/Domain/`)
   - Open `Entities/Building.cs` - Point out aggregate root
   - Show the `_lock` object for thread safety
   - Point to `ProcessTick()` method

2. **Value Objects** (`src/ElevatorSystem/Domain/ValueObjects/`)
   - Open `Direction.cs` - Show singleton pattern
   - Open `DestinationSet.cs` - Show immutability

3. **Design Patterns** (`src/ElevatorSystem/Domain/Services/`)
   - Open `ISchedulingStrategy.cs` - Strategy pattern interface
   - Open `DirectionAwareStrategy.cs` - Implementation

4. **Error Handling** (`src/ElevatorSystem/Common/`)
   - Open `Result.cs` - Functional error handling

**What to emphasize:**
- Thread safety via single lock
- Strategy pattern for pluggable algorithms
- Result<T> pattern for explicit error handling
- Immutable value objects

### Part 3: Running the Simulation (3 minutes)

**Terminal Demo:**

```bash
cd src/ElevatorSystem
dotnet run
```

**What happens:**
1. Configuration loaded from `appsettings.json`
2. System initializes 4 elevators
3. Random requests generated every 5 seconds
4. Elevators move and service hall calls
5. Metrics printed every 10 seconds

**Let it run for 30-60 seconds**

**What to narrate:**
> "The system is generating random hall call requests. Notice the logging shows:"
> - Request acceptance/rejection (rate limiting in action)
> - Elevator movements and door operations
> - Hall call assignments using direction-aware scheduling
> - Periodic metrics showing system throughput

**Point out:**
- Hall call assignment logic (direction-aware)
- Door timers (3 ticks = 3 seconds)
- Completed requests counter
- Rate limiting rejections (if any)

**Stop gracefully:**
```
Press Ctrl+C
```

**What to say:**
> "Notice the graceful shutdown - the system cleanly terminates without errors."

### Part 4: Configuration Demonstration (1 minute)

**Edit `src/ElevatorSystem/appsettings.json`:**

Change:
```json
{
  "TickIntervalMs": 1000,
  "RequestIntervalSeconds": 5
}
```

To:
```json
{
  "TickIntervalMs": 500,
  "RequestIntervalSeconds": 2
}
```

**What to say:**
> "Let me demonstrate configuration flexibility - speeding up the simulation."

**Run again:**
```bash
dotnet run
```

**Let it run for 20-30 seconds (it will be faster)**

**What to emphasize:**
- Configuration validation at startup
- System runs at double speed (500ms ticks)
- More frequent requests (every 2s instead of 5s)
- Rate limiting kicks in more often

Press `Ctrl+C` to stop.

### Part 5: Architecture Deep Dive (2 minutes - if time permits)

**Open key files and explain:**

1. **`Program.cs`** - Manual dependency injection
   ```csharp
   // Point out the manual DI setup
   var logger = new ConsoleLogger();
   var timeService = new SystemTimeService();
   var strategy = new DirectionAwareStrategy();
   // ... etc
   ```

2. **`Building.cs`** - Concurrency model
   ```csharp
   // Show the lock usage
   lock (_lock)
   {
       // All state mutations happen here
   }
   ```

3. **`Elevator.cs`** - State machine
   ```csharp
   // Point to state machine implementation
   public void ProcessTick() { ... }
   private void DecideNextAction() { ... }
   ```

4. **`RateLimiter.cs`** - Sliding window algorithm
   ```csharp
   // Explain the sliding window implementation
   private readonly Queue<DateTime> _globalRequests;
   ```

## Key Talking Points

### Design Decisions (Why This Approach?)

**Q: Why single lock instead of fine-grained locking?**
> "Single lock eliminates deadlocks entirely and simplifies reasoning. Performance analysis showed 0.04% contention - far below any concern threshold. Premature optimization would add complexity without benefit."

**Q: Why Clean Architecture for a simulation?**
> "Clean Architecture provides testability through dependency inversion. The domain layer has zero external dependencies, making it trivially testable. This also future-proofs the system - we can swap console logging for structured logging without touching business logic."

**Q: Why Strategy pattern for scheduling?**
> "Elevator scheduling is a known area for optimization and experimentation. The Strategy pattern makes algorithms pluggable without client code changes. We can add ML-based scheduling in Phase 2 without modifying Building.cs."

**Q: Why Result<T> instead of exceptions?**
> "Expected failures (invalid floor, rate limits) shouldn't use exceptions - they're not exceptional. Result<T> makes error handling explicit at compile time and performs better. We reserve exceptions for configuration validation (fail-fast) and programmer errors."

**Q: Why in-memory instead of database?**
> "Phase 1 requirements don't specify persistence. A database would add infrastructure complexity and make sub-microsecond latencies impossible. Event sourcing can be added in Phase 2 if audit trails are needed."

**Q: Why Direction-Aware scheduling?**
> "It mirrors real elevator behavior - continuing in direction before reversing minimizes wait times. A simple 'nearest elevator' algorithm would cause thrashing. The algorithm is realistic and efficient."

### Performance Metrics

**If asked about performance:**
> "The system exceeds requirements by 4-5 orders of magnitude:"
> - Request latency: ~10μs vs. <1s requirement (100,000× margin)
> - Throughput: 6M req/min vs. 20/min requirement (300,000× margin)
> - Lock contention: 0.04% (essentially zero)

**Why such high performance?**
> "All operations are in-memory with simple data structures. The critical path (request processing) is a lock acquisition, hash table lookup, and list traversal - all O(1) or O(n) with small n. No I/O, no network, no database."

### Testing Approach

**If asked about testing:**
> "Test pyramid approach: 80% unit, 20% integration, targeting 90% coverage. Time is abstracted via ITimeService - tests use FakeTimeService to run in milliseconds instead of seconds. This enables fast, deterministic testing of time-dependent logic."

### Future Improvements

**If asked about limitations:**
> "See docs/FUTURE-IMPROVEMENTS.md for complete roadmap. Priority items:"
> 1. **Observability** - Structured logging, Prometheus metrics, distributed tracing
> 2. **API Layer** - REST API for integration, WebSocket for real-time updates
> 3. **Security** - OAuth 2.0, RBAC, audit logging
> 4. **Features** - Destination dispatch, passenger capacity, priority requests

## Common Questions & Answers

**Q: How would you scale this to 100 buildings?**
> "Horizontal scaling by Building ID. Deploy independent instances per building with shared monitoring infrastructure. Each building is a bounded context - no cross-building coordination needed. Use Kubernetes for orchestration."

**Q: How would you add persistence?**
> "Event sourcing pattern. Store domain events (HallCallRequested, ElevatorMoved) instead of current state. State becomes projection of events. Enables audit trail, debugging, and time-travel queries. Use EventStore or PostgreSQL with event table."

**Q: What if an elevator gets stuck?**
> "Safety timeout implemented - if elevator doesn't progress after 10 ticks, transition is forced. In production, this would trigger alerts via monitoring system and dispatch maintenance."

**Q: How do you handle elevator capacity?**
> "Phase 1 has no capacity limits per requirements. Adding it requires: (1) MaxCapacity property on Elevator, (2) CurrentPassengerCount tracking, (3) CanAcceptHallCall() check for capacity. See FUTURE-IMPROVEMENTS.md Section 2.1."

**Q: Why C# instead of Java/Python?**
> "C# provides excellent concurrency primitives (lock, Monitor), strong typing with nullable reference types, modern language features (records, pattern matching), and great tooling. .NET 8 performance rivals native code. Personal preference - design principles apply regardless of language."

## Video Recording Suggestions

If you plan to record a demo video:

### Structure (10-15 minutes)

1. **Introduction (1 min)**
   - Project overview
   - Technology stack
   - Design methodology

2. **Code Walkthrough (5 min)**
   - Domain model overview
   - Key design patterns (Strategy, Result<T>)
   - Concurrency model (single lock)
   - Error handling approach

3. **Live Demo (3 min)**
   - Run simulation
   - Explain log output
   - Show metrics
   - Demonstrate graceful shutdown

4. **Architecture Discussion (4 min)**
   - Clean Architecture layers
   - Dependency flow
   - ADRs (why not alternatives?)

5. **Q&A Topics (2 min)**
   - Scalability approach
   - Future improvements
   - Design trade-offs

### Recording Tips

- Use screen recording software (OBS Studio, Camtasia)
- Set terminal font size to 16-18pt for readability
- Use light theme or high-contrast dark theme
- Rehearse 2-3 times before recording
- Keep pace steady - not too fast
- Pause briefly between major sections
- Consider picture-in-picture (your face in corner)

### IDE Setup for Recording

- Close unnecessary windows
- Increase font size (View → Zoom In)
- Hide minimap and unnecessary panels
- Use split view for code + terminal
- Prepare bookmarks for key files

## Post-Demo Discussion Points

After the demo, be prepared to discuss:

1. **Design Process** - Why 12 phases? What did each contribute?
2. **Trade-offs** - What did you sacrifice for simplicity?
3. **Alternatives** - What else did you consider?
4. **Production Readiness** - What's missing for production?
5. **Maintenance** - How would a team work on this?
6. **Extension** - How would you add feature X?

## Troubleshooting

**If simulation doesn't show much activity:**
- Reduce `RequestIntervalSeconds` to 2-3
- Increase `TickIntervalMs` to 1500-2000 (slower, easier to follow)

**If it's too fast to follow:**
- Increase `TickIntervalMs` to 2000
- Increase `RequestIntervalSeconds` to 8-10

**If rate limiting blocks everything:**
- Check that RequestIntervalSeconds is >= 3 (to stay under 20/min limit)

**If errors occur:**
- Check appsettings.json is valid JSON
- Verify values are within valid ranges
- Ensure .NET 8 SDK is installed (`dotnet --version`)

## Success Criteria

Your demo is successful if the reviewer understands:

1. ✅ System architecture (Clean Architecture, DDD)
2. ✅ Design decisions (with justifications)
3. ✅ Concurrency model (single lock, why sufficient)
4. ✅ Key algorithms (direction-aware scheduling)
5. ✅ Error handling approach (Result<T>)
6. ✅ Future extensibility (Strategy pattern, layered design)

The goal is not just to show working code, but to demonstrate depth of thought in the design process.
