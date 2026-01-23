# Elevator Control System

A production-grade elevator control system implementing a complete 12-phase system design process. Built with C# (.NET 8) using Clean Architecture and Domain-Driven Design principles.

## Quick Start

```bash
cd src/ElevatorSystem
dotnet build
dotnet run
```

Press `Ctrl+C` to gracefully stop the simulation.

## Project Structure

```
elevator-exercise/
├── src/ElevatorSystem/          # Implementation (35 files, ~2,250 LOC)
│   ├── Domain/                  # Business logic (entities, value objects, domain services)
│   ├── Application/             # Use cases and orchestration
│   ├── Infrastructure/          # Cross-cutting concerns (logging, config, metrics)
│   └── Program.cs               # Application entry point
│
├── docs/
│   ├── COMPLETE-DESIGN.md       # Complete system design specification
│   ├── FUTURE-IMPROVEMENTS.md   # Planned enhancements and roadmap
│   └── README.md                # Documentation navigation guide
│
├── DEMO-GUIDE.md                # Guide for demonstrating the system
└── README.md                    # This file
```

## Documentation

**Primary Document:** [`docs/COMPLETE-DESIGN.md`](docs/COMPLETE-DESIGN.md)

Complete system design covering problem analysis, requirements, domain modeling, architecture, algorithms, concurrency strategy, error handling, performance analysis, and testing approach.

## System Capabilities

- **Configurable Scale:** 4 elevators (1-10), 10 floors (2-100)
- **Intelligent Scheduling:** Direction-aware algorithm for optimal elevator selection
- **Thread-Safe Design:** Pessimistic locking with single global lock
- **Rate Limiting:** Global (20 req/min) and per-source (10 req/min) constraints
- **Observability:** Structured logging and real-time metrics (10s intervals)
- **Configuration Management:** JSON-based with validation and fail-fast behavior
- **Graceful Degradation:** Proper shutdown handling and error recovery

## Architecture

The system follows Clean Architecture principles with clear separation of concerns across three layers:

```
Infrastructure Layer (Technical Concerns)
    Logging, Configuration, Time Abstraction, Metrics
         ↓
Application Layer (Use Cases)
    Simulation Service, Request Generator, System Orchestrator
         ↓
Domain Layer (Business Logic)
    Building (Aggregate Root), Elevator, HallCall, Request
```

**Dependency Rule:** Domain layer has zero dependencies. Application layer depends only on Domain. Infrastructure layer provides implementations for both.

## Performance Characteristics

| Metric | Target | Actual | Margin |
|--------|--------|--------|--------|
| Request latency | <1 second | ~10μs | 100,000× |
| Status query | <1 second | ~50μs | 20,000× |
| Throughput | 20/min | 6M/min | 300,000× |
| Lock contention | N/A | 0.04% | Negligible |

## Configuration

System behavior is controlled via `src/ElevatorSystem/appsettings.json`:

```json
{
  "MaxFloors": 10,              // Valid range: 2-100
  "ElevatorCount": 4,           // Valid range: 1-10
  "TickIntervalMs": 1000,       // Valid range: 10-10000
  "DoorOpenTicks": 3,           // Valid range: 1-10
  "RequestIntervalSeconds": 5   // Valid range: 1-60
}
```

Invalid configuration triggers fail-fast behavior with descriptive error messages.

## Testing Strategy

Test pyramid approach targeting 90% code coverage:

```bash
cd tests/ElevatorSystem.Tests
dotnet test
```

**Distribution:**
- Unit Tests (70%): ~60 tests covering individual components
- Integration Tests (20%): ~15 tests validating component interactions
- End-to-End Tests (10%): ~5 tests exercising full system scenarios

**Framework:** xUnit with Moq for test doubles and time acceleration via `ITimeService` abstraction.

## Technology Stack

- **Runtime:** .NET 8
- **Language:** C# 12
- **Architecture:** Clean Architecture with DDD tactical patterns
- **Testing:** xUnit, Moq
- **Configuration:** System.Text.Json
- **Concurrency:** Monitor-based locking (pessimistic)

## Architecture Decision Records

### ADR-001: Clean Architecture

**Decision:** Adopt Clean Architecture with strict layer separation.

**Rationale:** 
- **Testability:** Domain logic isolated from infrastructure dependencies
- **Maintainability:** Clear boundaries reduce coupling and improve cohesion
- **Flexibility:** Infrastructure changes (e.g., different logging) don't affect business logic

**Trade-offs:**
- Additional abstraction layers increase initial complexity
- More files and interfaces to manage
- Accepted because benefits outweigh costs for production systems

### ADR-002: Single Lock Concurrency Model

**Decision:** Use single pessimistic lock at `Building` aggregate boundary.

**Rationale:**
- **Correctness:** Eliminates all race conditions by design
- **Simplicity:** No deadlock risk, easy to reason about
- **Sufficient Performance:** 0.04% lock contention, exceeds requirements by 100,000×

**Alternatives Considered:**
- **Fine-grained locking:** Complex, deadlock-prone, unnecessary given performance headroom
- **Optimistic concurrency:** Retry logic complexity not justified by performance requirements

**Trade-offs:**
- Potential bottleneck if scaling beyond 20+ elevators
- Accepted because Phase 1 requirements are single-building focused

### ADR-003: Direction-Aware Scheduling Strategy

**Decision:** Implement Strategy pattern with direction-aware default algorithm.

**Rationale:**
- **Realism:** Mirrors real elevator behavior (continue in direction before reversing)
- **Efficiency:** Minimizes passenger wait times and energy consumption
- **Extensibility:** Strategy pattern enables algorithm swapping without code changes

**Algorithm:**
1. Filter elevators moving in hall call direction
2. Select nearest elevator by floor distance
3. Fall back to idle elevators if no directional match

**Trade-offs:**
- More complex than simple "nearest elevator" approach
- Accepted because realism and efficiency gains justify complexity

### ADR-004: Result<T> Pattern for Error Handling

**Decision:** Use `Result<T>` pattern for domain operations instead of exceptions.

**Rationale:**
- **Explicit:** Caller must handle failure cases at compile time
- **Performance:** Avoids exception overhead for expected failures (invalid requests, rate limits)
- **Clarity:** Error cases documented in return type signature

**Exceptions Used For:**
- Configuration validation (fail-fast at startup)
- Programmer errors (contract violations)
- Unexpected infrastructure failures

**Trade-offs:**
- More verbose than exception-based code
- Accepted because explicit error handling improves reliability

### ADR-005: Domain-Driven Design Tactical Patterns

**Decision:** Apply DDD patterns (Entities, Value Objects, Aggregate Root, Domain Services).

**Rationale:**
- **Entities:** `Building`, `Elevator`, `HallCall` have identity and lifecycle
- **Value Objects:** `Direction`, `Journey`, `DestinationSet` are immutable and interchangeable
- **Aggregate Root:** `Building` enforces consistency boundaries
- **Domain Services:** `ISchedulingStrategy` encapsulates algorithms that don't belong to entities

**Trade-offs:**
- Steeper learning curve for developers unfamiliar with DDD
- Accepted because patterns match problem domain naturally

### ADR-006: In-Memory State Management

**Decision:** No database persistence, all state in memory.

**Rationale:**
- **Requirements:** Phase 1 has no persistence requirements
- **Simplicity:** Eliminates database infrastructure and transaction complexity
- **Performance:** Sub-microsecond latencies impossible with database I/O

**Future Consideration:** Phase 2+ may require persistence for audit logging or recovery.

**Trade-offs:**
- State lost on shutdown
- Accepted because requirements don't specify durability

## Project Status

- **Design:** Complete (12-phase methodology documented)
- **Implementation:** Complete (35 source files, ~2,250 LOC)
- **Documentation:** Complete (design specification + ADRs)
- **Testing:** Strategy defined, implementation in progress
