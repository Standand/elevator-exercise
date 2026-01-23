# Phase 12 - Testing Strategy

## Overview

Testing strategy for the elevator control system. We're using a test pyramid approach with unit tests as the foundation, integration tests for component interactions, and E2E tests for full scenarios.

Target coverage is 90% with fast, deterministic tests.

---

## Testing Pyramid

The distribution follows the standard pyramid:

- Unit tests: ~70% (120 tests implemented)
- Integration tests: ~20% (39 tests implemented)  
- E2E tests: ~10% (planned for future)

Unit tests run in under 50ms. Integration tests take around 200ms. The goal is fast feedback during development.

---

## Unit Test Coverage

### Critical Components

These need 100% coverage:

**DirectionAwareStrategy.SelectBestElevator()**
- Idle elevator selection (picks nearest)
- Moving elevator selection (prioritizes same direction)
- No available elevators (returns null)
- Multiple candidates (picks nearest among same direction)

**Elevator.CanAcceptHallCall()**
- IDLE state accepts any hall call
- MOVING same direction accepts if floor is between current and furthest destination
- MOVING opposite direction rejects
- LOADING at hall call floor rejects (duplicate)
- Edge cases: floor 0, top floor

**Building.RequestHallCall()**
- Valid request succeeds
- Invalid floor fails
- Invalid direction fails
- Rate limit exceeded fails
- Queue full fails
- Duplicate request returns existing (idempotent)

**DestinationSet.GetNextDestination()**
- UP direction returns smallest floor >= current
- DOWN direction returns largest floor <= current
- Floor 0 handling (critical edge case)
- IDLE direction returns nearest

**RateLimiter.IsAllowed()**
- Under global limit allows
- Over global limit rejects
- Per-source limits work independently
- Sliding window expires old requests

### Medium Priority

**ConfigurationLoader.Validate()**
- Valid config succeeds
- Invalid values throw with clear messages
- Multiple errors all reported

**HallCallQueue**
- Add, find, and retrieve operations
- Pending filtering works correctly

**Value Object Factories**
- Factory methods validate input
- Invalid values throw exceptions
- Immutability enforced

---

## Integration Test Scenarios

Integration tests verify components work together correctly.

**Full Request Lifecycle**
- Request created → Hall call assigned → Elevator moves → Passenger boards → Elevator moves to destination → Request completed

**Multi-Elevator Coordination**
- Multiple elevators operate independently
- Load balancing distributes requests
- No interference between elevators

**Concurrent Requests**
- Multiple threads can call RequestHallCall simultaneously
- No race conditions or data corruption
- State remains consistent

**Rate Limiting Integration**
- Global and per-source limits enforced
- Metrics updated correctly
- Rejected requests handled gracefully

**Tick Processing Sequence**
- Pending hall calls assigned before elevator processing
- Elevators processed in order
- Hall calls completed after elevator arrives
- Metrics updated at end of tick

---

## Test Tools

**xUnit** - Test framework. Clean syntax, widely used in .NET.

**Moq** - Mocking framework for dependencies like ILogger, ITimeService, IMetrics.

**MockTimeService** - Custom helper to control time in tests. Allows tests to run instantly instead of waiting for real delays.

---

## Test Organization

Tests mirror the source structure:

```
tests/ElevatorSystem.Tests/
├── Domain/
│   ├── Entities/
│   ├── ValueObjects/
│   └── Services/
├── Common/
├── Infrastructure/
└── Integration/
```

This makes it easy to find tests for any given class.

---

## Test Naming

Pattern: `MethodName_Scenario_ExpectedBehavior`

Examples:
- `SelectBestElevator_IdleElevator_SelectsNearest`
- `RequestHallCall_InvalidFloor_ReturnsFailure`
- `CanAcceptHallCall_MovingOppositeDirection_ReturnsFalse`

---

## Current Status

**Unit Tests:** 120 tests passing
- Building: 8 tests
- Elevator: 12 tests
- HallCall: 6 tests
- Request: 6 tests
- Value Objects: 33 tests
- Services: 12 tests
- Common: 14 tests
- Infrastructure: 27 tests

**Integration Tests:** 39 tests passing
- Building + Elevator + Scheduling: 8 tests
- Request Lifecycle: 6 tests
- Multi-Elevator Coordination: 5 tests
- Metrics Integration: 4 tests
- Rate Limiting: 3 tests
- Tick Processing: 5 tests
- Error Handling: 4 tests
- Concurrent Operations: 3 tests

All tests run in under 250ms total. No flaky tests.

---

## Running Tests

```bash
# All tests
cd tests/ElevatorSystem.Tests
dotnet test

# Unit tests only
dotnet test --filter "Category=Unit"

# Integration tests only
dotnet test --filter "Category=Integration"

# Specific test class
dotnet test --filter "FullyQualifiedName~BuildingTests"
```

---

## Test Helpers

**MockLogger** - Captures log messages for verification

**MockTimeService** - Controls time progression for deterministic tests

**TestBuilders** - Factory methods for creating test objects with sensible defaults

**IntegrationTestBuilder** - Builder for setting up integration test scenarios

**TickSimulator** - Helper to simulate multiple ticks quickly

**MetricsVerifier** - Helper to verify metrics changes

---

## Notes

Tests use AAA pattern (Arrange-Act-Assert) for clarity. Each test is isolated with no shared state. Time is controlled via MockTimeService to avoid real delays.

The test suite provides confidence for refactoring and ensures the system behaves correctly under various scenarios.
