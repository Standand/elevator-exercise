# Testing Quick Start Guide

**Goal:** Implement 85 tests achieving 90% coverage  
**Estimated Effort:** 40-60 hours (5 weeks part-time, 2 weeks full-time)  
**Framework:** xUnit + Moq

---

## Quick Setup (30 minutes)

### 1. Create Test Project

```bash
cd "D:\Learning\System Design\elevator-exercise"
dotnet new xunit -n ElevatorSystem.Tests -o tests/ElevatorSystem.Tests
cd tests/ElevatorSystem.Tests
dotnet add reference ../../src/ElevatorSystem/ElevatorSystem.csproj
dotnet add package Moq
dotnet add package FluentAssertions
```

### 2. Create Folder Structure

```
tests/ElevatorSystem.Tests/
├── Domain/
│   ├── Entities/
│   │   ├── BuildingTests.cs
│   │   ├── ElevatorTests.cs
│   │   ├── HallCallTests.cs
│   │   └── RequestTests.cs
│   ├── ValueObjects/
│   │   ├── DestinationSetTests.cs
│   │   └── ValueObjectTests.cs
│   └── Services/
│       └── DirectionAwareStrategyTests.cs
├── Common/
│   ├── ResultTests.cs
│   └── RateLimiterTests.cs
├── Infrastructure/
│   ├── Metrics/
│   │   └── SystemMetricsTests.cs
│   └── Configuration/
│       └── ConfigurationLoaderTests.cs
├── Integration/
│   ├── RequestLifecycleTests.cs
│   ├── ConcurrencyTests.cs
│   ├── SchedulingIntegrationTests.cs
│   └── ErrorHandlingTests.cs
├── E2E/
│   └── SimulationScenariosTests.cs
└── TestHelpers/
    ├── TestBuilders.cs
    ├── MockLogger.cs
    └── MockTimeService.cs
```

### 3. Implement Test Helpers (1 hour)

Create these three helper files first - they're used by all tests.

---

## Test Priority Order

### Week 1: Critical Business Logic (16-20 hours)

**Priority 1: Building Tests (8 tests, 3-4 hours)**
```csharp
✅ RequestHallCall_ValidRequest_ReturnsSuccess
✅ RequestHallCall_FloorOutOfRange_ReturnsFailure
✅ RequestHallCall_InvalidDirection_ReturnsFailure
✅ RequestHallCall_DuplicateRequest_ReturnsExistingHallCall
✅ RequestHallCall_RateLimitExceeded_ReturnsFailure
✅ RequestHallCall_QueueAtCapacity_ReturnsFailure
✅ ProcessTick_PendingHallCall_GetsAssigned
✅ GetStatus_ReturnsCurrentState
```

**Priority 1: Elevator Tests (12 tests, 5-6 hours)**
```csharp
✅ ProcessTick_IdleWithDestinations_TransitionsToMoving
✅ ProcessTick_Moving_AdvancesOneFloor
✅ ProcessTick_ArrivesAtDestination_TransitionsToLoading
✅ ProcessTick_LoadingDoorTimerExpires_TransitionsToIdle
✅ ProcessTick_LoadingWithMoreDestinations_ContinuesMoving
✅ ProcessTick_StuckInLoading_SafetyTimeoutForcesTransition
✅ CanAcceptHallCall_IdleElevator_ReturnsTrue
✅ CanAcceptHallCall_SameDirectionBetweenCurrentAndFurthest_ReturnsTrue
✅ CanAcceptHallCall_OppositeDirection_ReturnsFalse
✅ CanAcceptHallCall_AtCurrentFloorInLoading_ReturnsFalse
✅ AssignHallCall_AddsDestinationAndHallCallId
✅ RemoveHallCallId_RemovesFromAssignedList
```

---

### Week 2: Domain Logic (12-16 hours)

**Priority 2: DestinationSet Tests (6 tests, 2 hours)**
**Priority 2: Scheduling Tests (4 tests, 1.5 hours)**
**Priority 2: Value Objects (5 tests, 1.5 hours)**
**Priority 4: Entity State (5 tests, 2 hours)**

---

### Week 3: Infrastructure (8-10 hours)

**Priority 3: Result<T> + RateLimiter (7 tests, 3 hours)**
**Priority 3: Metrics (3 tests, 1 hour)**
**Priority 5: Configuration (5 tests, 2 hours)**

---

### Week 4: Integration (12-14 hours)

**15 Integration Tests**
- Request lifecycle (3 tests)
- Concurrency (5 tests)
- Scheduling integration (4 tests)
- Error handling (3 tests)

---

### Week 5: End-to-End (8-10 hours)

**5 E2E Tests**
- Rush hour scenario
- Single elevator
- Opposite directions
- Graceful degradation
- Mixed load

---

## Essential Test Helpers

### TestBuilders.cs (Copy & Paste Ready)

```csharp
using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.Services;
using ElevatorSystem.Infrastructure.Configuration;
using ElevatorSystem.Infrastructure.Metrics;
using ElevatorSystem.Common;

namespace ElevatorSystem.Tests.TestHelpers
{
    public static class TestBuilders
    {
        public static Building CreateBuilding(
            ISchedulingStrategy? strategy = null,
            int elevatorCount = 4,
            int maxFloors = 10,
            int doorOpenTicks = 3)
        {
            var logger = new MockLogger();
            var metrics = new SystemMetrics();
            var rateLimiter = new RateLimiter(20, 10, logger);
            var config = new SimulationConfiguration
            {
                MaxFloors = maxFloors,
                ElevatorCount = elevatorCount,
                DoorOpenTicks = doorOpenTicks,
                TickIntervalMs = 1000,
                RequestIntervalSeconds = 5
            };
            
            return new Building(
                strategy ?? new DirectionAwareStrategy(),
                logger,
                metrics,
                rateLimiter,
                config);
        }
        
        public static Elevator CreateElevator(
            int id = 1,
            int currentFloor = 0,
            int maxFloors = 10,
            int doorOpenTicks = 3)
        {
            var logger = new MockLogger();
            return new Elevator(id, maxFloors, doorOpenTicks, logger);
        }
    }
}
```

### MockLogger.cs (Copy & Paste Ready)

```csharp
using System.Collections.Generic;
using System.Linq;
using ElevatorSystem.Infrastructure.Logging;

namespace ElevatorSystem.Tests.TestHelpers
{
    public class MockLogger : ILogger
    {
        public List<string> Messages { get; } = new List<string>();
        
        public void LogDebug(string message) => Messages.Add($"[DEBUG] {message}");
        public void LogInfo(string message) => Messages.Add($"[INFO] {message}");
        public void LogWarning(string message) => Messages.Add($"[WARN] {message}");
        public void LogError(string message) => Messages.Add($"[ERROR] {message}");
        
        public void Clear() => Messages.Clear();
        
        public bool Contains(string substring) => 
            Messages.Any(m => m.Contains(substring));
    }
}
```

### MockTimeService.cs (Copy & Paste Ready)

```csharp
using System;
using ElevatorSystem.Infrastructure.Time;

namespace ElevatorSystem.Tests.TestHelpers
{
    public class MockTimeService : ITimeService
    {
        private DateTime _currentTime = DateTime.UtcNow;
        
        public DateTime UtcNow => _currentTime;
        
        public void AdvanceTime(TimeSpan duration)
        {
            _currentTime = _currentTime.Add(duration);
        }
        
        public void SetTime(DateTime time)
        {
            _currentTime = time;
        }
        
        public void Reset()
        {
            _currentTime = DateTime.UtcNow;
        }
    }
}
```

---

## Sample Test Template

### BuildingTests.cs (Copy & Paste to Start)

```csharp
using Xunit;
using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.ValueObjects;
using ElevatorSystem.Tests.TestHelpers;

namespace ElevatorSystem.Tests.Domain.Entities
{
    public class BuildingTests
    {
        [Fact]
        [Trait("Category", "Unit")]
        public void RequestHallCall_ValidRequest_ReturnsSuccess()
        {
            // Arrange
            var building = TestBuilders.CreateBuilding();
            
            // Act
            var result = building.RequestHallCall(5, Direction.UP, "TestSource");
            
            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Value);
            Assert.Equal(5, result.Value.Floor);
            Assert.Equal(Direction.UP, result.Value.Direction);
        }
        
        [Theory]
        [InlineData(-1)]
        [InlineData(11)]
        [InlineData(100)]
        [Trait("Category", "Unit")]
        public void RequestHallCall_FloorOutOfRange_ReturnsFailure(int invalidFloor)
        {
            // Arrange
            var building = TestBuilders.CreateBuilding(maxFloors: 10);
            
            // Act
            var result = building.RequestHallCall(invalidFloor, Direction.UP);
            
            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("out of range", result.Error);
        }
        
        // Add remaining 6 tests here...
    }
}
```

---

## Running Tests

```bash
# Run all tests
dotnet test

# Run only unit tests
dotnet test --filter "Category=Unit"

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# Run specific test
dotnet test --filter "FullyQualifiedName~BuildingTests.RequestHallCall_ValidRequest"

# Watch mode (re-run on file change)
dotnet watch test
```

---

## Test Checklist

### ✅ Test Quality Checklist
- [ ] Follows AAA pattern (Arrange-Act-Assert)
- [ ] One logical assertion per test
- [ ] Descriptive name: `MethodName_Scenario_ExpectedBehavior`
- [ ] Uses test helpers (no duplication)
- [ ] No Thread.Sleep() (use MockTimeService)
- [ ] No external dependencies (all mocked)
- [ ] Independent (can run in any order)
- [ ] Fast execution (< 100ms per test)
- [ ] Has `[Trait("Category", "...")]` attribute

### ✅ Coverage Checklist
- [ ] Happy path tested
- [ ] Error cases tested
- [ ] Edge cases tested (floor 0, rate limits, queue full)
- [ ] State transitions tested
- [ ] Concurrency tested (where applicable)
- [ ] Null/invalid input tested

---

## Common Patterns

### Pattern 1: Testing State Machines

```csharp
[Fact]
public void ProcessTick_StateTransition_UpdatesCorrectly()
{
    // Arrange
    var elevator = TestBuilders.CreateElevator(currentFloor: 0);
    elevator.AddDestination(5);
    
    // Act & Assert - Verify each state transition
    Assert.Equal(ElevatorState.IDLE, elevator.State);
    
    elevator.ProcessTick(); // IDLE → MOVING
    Assert.Equal(ElevatorState.MOVING, elevator.State);
    Assert.Equal(Direction.UP, elevator.Direction);
    
    // Continue until arrived...
}
```

### Pattern 2: Testing Concurrency

```csharp
[Fact]
public void Method_ConcurrentCalls_ThreadSafe()
{
    // Arrange
    var building = TestBuilders.CreateBuilding();
    var tasks = new List<Task>();
    
    // Act - Spawn 100 threads
    for (int i = 0; i < 100; i++)
    {
        int floor = i % 10;
        tasks.Add(Task.Run(() => 
            building.RequestHallCall(floor, Direction.UP, $"Source{i}")));
    }
    Task.WaitAll(tasks.ToArray());
    
    // Assert - No exceptions, consistent state
    var status = building.GetStatus();
    Assert.NotNull(status);
}
```

### Pattern 3: Testing with Time

```csharp
[Fact]
public void RateLimiter_AfterTimeWindow_AllowsNewRequests()
{
    // Arrange
    var mockTime = new MockTimeService();
    var rateLimiter = new RateLimiter(1, 1, new MockLogger());
    
    // Act
    rateLimiter.IsAllowed("Source1"); // First request
    mockTime.AdvanceTime(TimeSpan.FromMinutes(2)); // Skip ahead
    var result = rateLimiter.IsAllowed("Source1"); // Should work
    
    // Assert
    Assert.True(result);
}
```

---

## Troubleshooting

### Issue: Tests fail intermittently
**Solution:** Remove Thread.Sleep(), use MockTimeService

### Issue: Tests are slow
**Solution:** Mock external dependencies, use in-memory data

### Issue: Tests are coupled
**Solution:** Each test should create its own instances

### Issue: Hard to understand failures
**Solution:** Add descriptive assertion messages:
```csharp
Assert.True(result.IsSuccess, $"Expected success but got: {result.Error}");
```

---

## Progress Tracking

Use this checklist to track your progress:

```
Week 1: Critical Business Logic
[ ] Test infrastructure setup (4 hours)
[ ] Building tests (8 tests, 3-4 hours)
[ ] Elevator tests (12 tests, 5-6 hours)
Total: 20 tests

Week 2: Domain Logic
[ ] DestinationSet tests (6 tests, 2 hours)
[ ] Scheduling tests (4 tests, 1.5 hours)
[ ] Value object tests (5 tests, 1.5 hours)
[ ] Entity state tests (5 tests, 2 hours)
Total: 40 tests

Week 3: Infrastructure
[ ] Result + RateLimiter (7 tests, 3 hours)
[ ] Metrics tests (3 tests, 1 hour)
[ ] Configuration tests (5 tests, 2 hours)
Total: 55 tests

Week 4: Integration
[ ] Request lifecycle (3 tests, 2 hours)
[ ] Concurrency tests (5 tests, 3 hours)
[ ] Scheduling integration (4 tests, 2 hours)
[ ] Error handling (3 tests, 1.5 hours)
Total: 70 tests

Week 5: End-to-End
[ ] E2E scenarios (5 tests, 4-5 hours)
[ ] Documentation & polish (2 hours)
Total: 85 tests ✅
```

---

## Success Metrics

**Target:**
- ✅ 85+ tests implemented
- ✅ 90% code coverage
- ✅ All tests pass consistently
- ✅ Test execution < 30 seconds
- ✅ Zero flaky tests

**Current:**
- ⏳ 0 tests (Ready to start!)
- ⏳ 0% coverage
- ⏳ Test infrastructure needed

---

## Next Steps

1. **Today:** Setup test project (30 minutes)
2. **This Week:** Implement test helpers + 20 critical tests (16-20 hours)
3. **Week 2-5:** Continue with roadmap

**Full plan:** See `UNIT-TESTING-PLAN.md` for complete details

---

**Good luck! 🚀**
