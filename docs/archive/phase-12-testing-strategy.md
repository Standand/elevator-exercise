# Phase 12 - Testing Strategy

## Overview

This document defines the comprehensive testing strategy for the Elevator Control System, including unit tests, integration tests, test organization, and testing tools.

**Testing Philosophy:** High coverage (90%), fast feedback, deterministic tests

---

## 1. Testing Pyramid

### Distribution

```
        /\
       /  \      E2E Tests (10%)
      /____\     ~5 tests
     /      \    
    /        \   Integration Tests (20%)
   /__________\  ~15 tests
  /            \
 /              \ Unit Tests (70%)
/________________\ ~60 tests

Total: ~80 tests
```

### Breakdown

| Test Type | Percentage | Count | Speed | Purpose |
|-----------|-----------|-------|-------|---------|
| **Unit Tests** | 70% | ~60 | Fast (<1ms) | Test individual classes in isolation |
| **Integration Tests** | 20% | ~15 | Medium (~100ms) | Test component interactions |
| **E2E Tests** | 10% | ~5 | Slow (~1s) | Test full system scenarios |

**Target Coverage:** 90% code coverage

---

## 2. Unit Test Coverage

### High Priority (Critical Logic)

**Must have 100% coverage:**

1. **`DirectionAwareStrategy.SelectBestElevator()`**
   - Test: Idle elevator selection (nearest)
   - Test: Moving elevator selection (same direction)
   - Test: No available elevators (returns null)
   - Test: Multiple candidates (picks nearest)

2. **`Elevator.CanAcceptHallCall()`**
   - Test: IDLE state (accepts any)
   - Test: MOVING same direction (accepts if between current and furthest)
   - Test: MOVING opposite direction (rejects)
   - Test: LOADING at hall call floor (rejects - duplicate)
   - Test: Edge case - floor 0, top floor

3. **`Building.RequestHallCall()`**
   - Test: Valid request (success)
   - Test: Invalid floor (failure)
   - Test: Invalid direction (failure)
   - Test: Rate limit exceeded (failure)
   - Test: Queue full (failure)
   - Test: Duplicate request (idempotent - returns existing)

4. **`DestinationSet.GetNextDestination()`**
   - Test: UP direction (smallest >= current)
   - Test: DOWN direction (largest <= current)
   - Test: Floor 0 handling (critical - was a bug!)
   - Test: Wrap around (no candidates in direction)
   - Test: IDLE (nearest)

5. **`RateLimiter.IsAllowed()`**
   - Test: Under global limit (allowed)
   - Test: Over global limit (rejected)
   - Test: Under per-source limit (allowed)
   - Test: Over per-source limit (rejected)
   - Test: Sliding window (old requests expire)

### Medium Priority

**Should have 80%+ coverage:**

6. **`ConfigurationLoader.Validate()`**
   - Test: Valid config (success)
   - Test: Invalid MaxFloors (throws)
   - Test: Invalid ElevatorCount (throws)
   - Test: Invalid TickIntervalMs (throws)
   - Test: Multiple validation errors (all reported)

7. **`HallCallQueue`**
   - Test: Add hall call
   - Test: Find by floor and direction
   - Test: Get pending (filters by status)
   - Test: Get pending count

8. **Value Object Factories**
   - Test: `Direction.Of("UP")` (success)
   - Test: `Direction.Of("INVALID")` (throws)
   - Test: `Journey.Of(3, 7)` (success)
   - Test: `Journey.Of(3, 3)` (throws - same floor)

### Low Priority (Simple)

**Optional coverage:**

9. **`ConsoleLogger`**
   - Test: LogInfo writes to console (hard to test, low value)

10. **`SystemTimeService`**
    - Test: GetCurrentTime returns DateTime.UtcNow (trivial)

---

## 3. Integration Test Scenarios

### Scenario 1: Happy Path (End-to-End)
**Test:** 10 requests, all completed successfully

```csharp
[Fact]
public async Task FullSimulation_10Requests_AllCompleted()
{
    // Arrange
    var config = CreateTestConfig(tickIntervalMs: 100); // Fast for testing
    var building = CreateBuilding(config);
    var simulation = CreateSimulation(building, config);
    
    // Act: Generate 10 requests
    for (int i = 0; i < 10; i++)
    {
        building.RequestHallCall(i % 10, Direction.UP);
    }
    
    // Run simulation for 30 seconds (accelerated time)
    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    await simulation.RunAsync(cts.Token);
    
    // Assert: All requests completed
    var status = building.GetStatus();
    Assert.Equal(0, status.PendingHallCallsCount);
}
```

### Scenario 2: Concurrent Requests
**Test:** Multiple threads calling RequestHallCall simultaneously

```csharp
[Fact]
public void ConcurrentRequests_10Threads_NoExceptions()
{
    // Arrange
    var building = CreateBuilding();
    var tasks = new List<Task>();
    
    // Act: Fire 100 requests from 10 threads
    for (int i = 0; i < 10; i++)
    {
        int threadId = i;
        tasks.Add(Task.Run(() =>
        {
            for (int j = 0; j < 10; j++)
            {
                building.RequestHallCall(j % 10, Direction.UP, $"Thread{threadId}");
            }
        }));
    }
    
    Task.WaitAll(tasks.ToArray());
    
    // Assert: No exceptions thrown, state is consistent
    var status = building.GetStatus();
    Assert.True(status.PendingHallCallsCount <= 18); // Max capacity
}
```

### Scenario 3: Rate Limiting
**Test:** Flood with 30 requests, verify 20 accepted, 10 rejected

```csharp
[Fact]
public void RateLimiting_30Requests_20Accepted10Rejected()
{
    // Arrange
    var building = CreateBuilding();
    var acceptedCount = 0;
    var rejectedCount = 0;
    
    // Act: Fire 30 requests rapidly
    for (int i = 0; i < 30; i++)
    {
        var result = building.RequestHallCall(i % 10, Direction.UP);
        if (result.IsSuccess)
            acceptedCount++;
        else
            rejectedCount++;
    }
    
    // Assert: 20 accepted, 10 rejected (global limit)
    Assert.Equal(20, acceptedCount);
    Assert.Equal(10, rejectedCount);
}
```

### Scenario 4: No Elevator Available
**Test:** All elevators busy, hall call stays PENDING

```csharp
[Fact]
public void NoElevatorAvailable_HallCallStaysPending()
{
    // Arrange
    var building = CreateBuilding();
    
    // Make all elevators busy (moving DOWN)
    for (int i = 1; i <= 4; i++)
    {
        // Assign each elevator a destination going DOWN
        building.RequestHallCall(0, Direction.DOWN); // Floor 0, going DOWN
    }
    
    // Act: Request UP from floor 5 (no elevator can accept)
    var result = building.RequestHallCall(5, Direction.UP);
    
    // Process one tick (try to assign)
    building.ProcessTick();
    
    // Assert: Hall call created but still PENDING
    Assert.True(result.IsSuccess);
    var status = building.GetStatus();
    Assert.Equal(1, status.PendingHallCallsCount);
}
```

### Scenario 5: Graceful Shutdown (Optional)
**Test:** Ctrl+C during active simulation

```csharp
[Fact]
public async Task GracefulShutdown_ActiveSimulation_CompletesWithin5Seconds()
{
    // Arrange
    var orchestrator = CreateOrchestrator();
    var cts = new CancellationTokenSource();
    
    // Act: Start system
    var startTask = orchestrator.StartAsync(cts.Token);
    
    // Wait 2 seconds, then cancel
    await Task.Delay(2000);
    cts.Cancel();
    
    // Assert: Completes within 5 seconds (shutdown timeout)
    var completed = await Task.WhenAny(startTask, Task.Delay(6000));
    Assert.Equal(startTask, completed); // Should complete before 6 second timeout
}
```

---

## 4. Test Doubles Strategy

### Mocking Framework: **Moq**

**Rationale:** Industry standard, easy to use, well-documented

**Installation:**
```bash
dotnet add package Moq
```

### Mock Examples

#### ILogger Mock
```csharp
var mockLogger = new Mock<ILogger>();
mockLogger.Setup(l => l.LogInfo(It.IsAny<string>()));

// Verify logging
mockLogger.Verify(l => l.LogInfo(It.Is<string>(s => s.Contains("HallCall"))), Times.Once);
```

#### ITimeService Mock (Time Acceleration)
```csharp
var mockTime = new Mock<ITimeService>();
var currentTime = DateTime.UtcNow;
mockTime.Setup(t => t.GetCurrentTime()).Returns(() => currentTime);

// Advance time in tests
currentTime = currentTime.AddSeconds(10);
```

#### IMetrics Mock
```csharp
var mockMetrics = new Mock<IMetrics>();
mockMetrics.Setup(m => m.IncrementTotalRequests());

// Verify metrics called
mockMetrics.Verify(m => m.IncrementTotalRequests(), Times.Exactly(10));
```

---

## 5. Time Acceleration for Tests

### Problem
Real-time tests are slow:
- 1 tick = 1 second
- Door open = 3 seconds
- Full journey = 10+ seconds

### Solution: Mock ITimeService

```csharp
public class FakeTimeService : ITimeService
{
    private DateTime _currentTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    
    public DateTime GetCurrentTime() => _currentTime;
    
    public void Advance(TimeSpan duration)
    {
        _currentTime += duration;
    }
    
    public void AdvanceTicks(int ticks, int tickIntervalMs)
    {
        _currentTime = _currentTime.AddMilliseconds(ticks * tickIntervalMs);
    }
}
```

### Usage in Tests

```csharp
[Fact]
public void ElevatorMovement_3Floors_Takes3Ticks()
{
    // Arrange
    var fakeTime = new FakeTimeService();
    var elevator = new Elevator(1, 10, 3, mockLogger.Object);
    elevator.AddDestination(3);
    
    // Act: Process 3 ticks (3 floors)
    for (int i = 0; i < 3; i++)
    {
        elevator.ProcessTick();
        fakeTime.Advance(TimeSpan.FromSeconds(1));
    }
    
    // Assert: Elevator at floor 3
    Assert.Equal(3, elevator.CurrentFloor);
}
```

**Benefits:**
- ✅ Tests run instantly (no real delays)
- ✅ Full control over time progression
- ✅ Test edge cases (timeouts, expiration)

---

## 6. Concurrency Testing

### Strategy: Hybrid Approach

#### Test 1: Simple Smoke Test (Always Run)
```csharp
[Fact]
public void ConcurrentRequests_10Threads_NoExceptions()
{
    // Arrange
    var building = CreateBuilding();
    var exceptions = new List<Exception>();
    
    // Act: 10 threads, 10 requests each
    Parallel.For(0, 10, i =>
    {
        try
        {
            for (int j = 0; j < 10; j++)
            {
                building.RequestHallCall(j % 10, Direction.UP);
            }
        }
        catch (Exception ex)
        {
            lock (exceptions)
            {
                exceptions.Add(ex);
            }
        }
    });
    
    // Assert: No exceptions
    Assert.Empty(exceptions);
}
```

#### Test 2: Stress Test (Manual Run)
```csharp
[Fact(Skip = "Manual stress test - run before releases")]
public void ConcurrentRequests_StressTest_1000Requests()
{
    // Arrange
    var building = CreateBuilding();
    var successCount = 0;
    var failureCount = 0;
    var lockObj = new object();
    
    // Act: 100 threads, 10 requests each = 1000 total
    Parallel.For(0, 100, i =>
    {
        for (int j = 0; j < 10; j++)
        {
            var result = building.RequestHallCall(j % 10, Direction.UP);
            lock (lockObj)
            {
                if (result.IsSuccess)
                    successCount++;
                else
                    failureCount++;
            }
        }
    });
    
    // Assert: All requests processed, no crashes
    Assert.Equal(1000, successCount + failureCount);
    
    // Verify state consistency
    var status = building.GetStatus();
    Assert.True(status.PendingHallCallsCount <= 18); // Max capacity
}
```

**Rationale:**
- Simple test runs in CI/CD (fast, deterministic)
- Stress test available for manual verification
- Best of both worlds

---

## 7. Performance Testing

### Decision: Skip for Phase 1

**Rationale:**
- Phase 9 analysis already validated performance (10μs request latency)
- Single lock design is simple and correct
- No performance issues expected
- Can add later if needed

**Future:** If performance becomes a concern, add benchmarks using `BenchmarkDotNet`.

---

## 8. Test Data Creation

### Strategy: Direct Construction

**Rationale:** Simple, clear, no extra abstractions needed

```csharp
// Good: Direct construction
var hallCall = new HallCall(5, Direction.UP);
var elevator = new Elevator(1, 10, 3, mockLogger.Object);
var config = new SimulationConfiguration
{
    MaxFloors = 10,
    ElevatorCount = 4,
    TickIntervalMs = 100,
    DoorOpenTicks = 3,
    RequestIntervalSeconds = 5
};
```

**Avoid:** Test builders (overkill for this project)
```csharp
// Overkill
var hallCall = HallCallBuilder.Create()
    .AtFloor(5)
    .GoingUp()
    .Build();
```

---

## 9. Test Organization

### Structure: By Class (Mirror src/)

```
tests/
├── ElevatorSystem.Tests.csproj
├── Common/
│   ├── ResultTests.cs
│   └── RateLimiterTests.cs
│
├── Domain/
│   ├── Entities/
│   │   ├── BuildingTests.cs
│   │   ├── ElevatorTests.cs
│   │   ├── HallCallTests.cs
│   │   └── RequestTests.cs
│   │
│   ├── ValueObjects/
│   │   ├── DirectionTests.cs
│   │   ├── JourneyTests.cs
│   │   ├── DestinationSetTests.cs
│   │   └── HallCallQueueTests.cs
│   │
│   └── Services/
│       ├── DirectionAwareStrategyTests.cs
│       └── NearestFirstStrategyTests.cs
│
├── Infrastructure/
│   ├── Configuration/
│   │   └── ConfigurationLoaderTests.cs
│   │
│   └── Metrics/
│       └── SystemMetricsTests.cs
│
└── Integration/
    ├── FullSimulationTests.cs
    ├── ConcurrencyTests.cs
    ├── RateLimitingTests.cs
    └── GracefulShutdownTests.cs
```

**Benefits:**
- ✅ Mirrors source structure (easy to navigate)
- ✅ Clear separation (unit vs integration)
- ✅ Easy to find tests for a given class

---

## 10. Assertion Strategy

### Framework: **xUnit**

**Rationale:** Modern, used by .NET team, clean syntax

**Installation:**
```bash
dotnet new xunit -n ElevatorSystem.Tests
cd ElevatorSystem.Tests
dotnet add reference ../ElevatorSystem/ElevatorSystem.csproj
dotnet add package Moq
```

### Assertion Examples

```csharp
// Equality
Assert.Equal(5, elevator.CurrentFloor);
Assert.NotEqual(Direction.IDLE, elevator.Direction);

// Boolean
Assert.True(result.IsSuccess);
Assert.False(hallCall.Status == HallCallStatus.COMPLETED);

// Null checks
Assert.NotNull(result.Value);
Assert.Null(scheduler.SelectBestElevator(hallCall, elevators));

// Collections
Assert.Empty(destinations);
Assert.Single(elevators, e => e.State == ElevatorState.IDLE);
Assert.Contains(hallCall, hallCallQueue.GetPending());

// Exceptions
Assert.Throws<ArgumentException>(() => Direction.Of("INVALID"));
Assert.Throws<InvalidOperationException>(() => destinationSet.GetNextDestination(0));

// Ranges
Assert.InRange(elevator.CurrentFloor, 0, 10);
```

---

## 11. Test Naming Convention

### Pattern: `MethodName_Scenario_ExpectedBehavior`

```csharp
// Good examples
[Fact]
public void SelectBestElevator_IdleElevator_SelectsNearest() { }

[Fact]
public void RequestHallCall_InvalidFloor_ReturnsFailure() { }

[Fact]
public void CanAcceptHallCall_MovingOppositeDirection_ReturnsFalse() { }

[Fact]
public void GetNextDestination_Floor0_ReturnsCorrectFloor() { }
```

**Benefits:**
- ✅ Clear what is being tested
- ✅ Clear what scenario
- ✅ Clear expected outcome

---

## 12. Test Execution

### Run All Tests
```bash
cd tests/ElevatorSystem.Tests
dotnet test
```

### Run Specific Test
```bash
dotnet test --filter "FullyQualifiedName~BuildingTests"
```

### Run with Coverage
```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

### Run in Watch Mode
```bash
dotnet watch test
```

---

## 13. Continuous Integration

### GitHub Actions Example

```yaml
name: Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v2
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v1
      with:
        dotnet-version: 8.0.x
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Build
      run: dotnet build --no-restore
    
    - name: Test
      run: dotnet test --no-build --verbosity normal
    
    - name: Coverage
      run: dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

---

## 14. Testing Checklist

### Before Committing
- [ ] All tests pass (`dotnet test`)
- [ ] No skipped tests (except manual stress tests)
- [ ] Code coverage ≥ 90%
- [ ] No compiler warnings

### Before Release
- [ ] Run manual stress tests
- [ ] Test on Windows, Linux, macOS
- [ ] Test with different configurations
- [ ] Verify graceful shutdown works

---

## 15. Test Examples (Detailed)

### Example 1: DirectionAwareStrategy Tests

```csharp
public class DirectionAwareStrategyTests
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly DirectionAwareStrategy _strategy;
    
    public DirectionAwareStrategyTests()
    {
        _mockLogger = new Mock<ILogger>();
        _strategy = new DirectionAwareStrategy();
    }
    
    [Fact]
    public void SelectBestElevator_IdleElevator_SelectsNearest()
    {
        // Arrange
        var elevator1 = CreateElevator(1, currentFloor: 0, state: ElevatorState.IDLE);
        var elevator2 = CreateElevator(2, currentFloor: 8, state: ElevatorState.IDLE);
        var elevators = new List<Elevator> { elevator1, elevator2 };
        var hallCall = new HallCall(5, Direction.UP);
        
        // Act
        var result = _strategy.SelectBestElevator(hallCall, elevators);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Id); // Elevator 1 is closer (5 floors vs 3 floors)
    }
    
    [Fact]
    public void SelectBestElevator_NoAvailableElevators_ReturnsNull()
    {
        // Arrange
        var elevator1 = CreateElevator(1, currentFloor: 0, direction: Direction.DOWN, state: ElevatorState.MOVING);
        var elevators = new List<Elevator> { elevator1 };
        var hallCall = new HallCall(5, Direction.UP);
        
        // Act
        var result = _strategy.SelectBestElevator(hallCall, elevators);
        
        // Assert
        Assert.Null(result);
    }
    
    private Elevator CreateElevator(int id, int currentFloor, ElevatorState state, Direction? direction = null)
    {
        var elevator = new Elevator(id, 10, 3, _mockLogger.Object);
        // Set state using reflection or test helper methods
        return elevator;
    }
}
```

### Example 2: Building.RequestHallCall Tests

```csharp
public class BuildingTests
{
    [Fact]
    public void RequestHallCall_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var building = CreateBuilding();
        
        // Act
        var result = building.RequestHallCall(5, Direction.UP);
        
        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(5, result.Value.Floor);
        Assert.Equal(Direction.UP, result.Value.Direction);
    }
    
    [Fact]
    public void RequestHallCall_InvalidFloor_ReturnsFailure()
    {
        // Arrange
        var building = CreateBuilding();
        
        // Act
        var result = building.RequestHallCall(15, Direction.UP); // Max floors = 10
        
        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("out of range", result.Error);
    }
    
    [Fact]
    public void RequestHallCall_RateLimitExceeded_ReturnsFailure()
    {
        // Arrange
        var building = CreateBuilding();
        
        // Flood with 25 requests
        for (int i = 0; i < 25; i++)
        {
            building.RequestHallCall(i % 10, Direction.UP);
        }
        
        // Act (21st request should be rate limited)
        var result = building.RequestHallCall(5, Direction.UP);
        
        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Rate limit exceeded", result.Error);
    }
    
    [Fact]
    public void RequestHallCall_DuplicateRequest_ReturnsExisting()
    {
        // Arrange
        var building = CreateBuilding();
        var firstResult = building.RequestHallCall(5, Direction.UP);
        
        // Act
        var secondResult = building.RequestHallCall(5, Direction.UP);
        
        // Assert
        Assert.True(secondResult.IsSuccess);
        Assert.Equal(firstResult.Value.Id, secondResult.Value.Id); // Same hall call
    }
    
    private Building CreateBuilding()
    {
        var config = new SimulationConfiguration
        {
            MaxFloors = 10,
            ElevatorCount = 4,
            TickIntervalMs = 1000,
            DoorOpenTicks = 3,
            RequestIntervalSeconds = 5
        };
        
        var mockLogger = new Mock<ILogger>();
        var mockMetrics = new Mock<IMetrics>();
        var rateLimiter = new RateLimiter(20, 10, mockLogger.Object);
        var strategy = new DirectionAwareStrategy();
        
        return new Building(strategy, mockLogger.Object, mockMetrics.Object, rateLimiter, config);
    }
}
```

---

## 16. Coverage Goals

| Component | Target Coverage | Priority |
|-----------|----------------|----------|
| **Domain Entities** | 95% | Critical |
| **Domain Services** | 95% | Critical |
| **Domain Value Objects** | 90% | High |
| **Application Services** | 80% | Medium |
| **Infrastructure** | 70% | Low |
| **Overall** | **90%** | **Target** |

---

## Phase 12 Complete ✅

**Testing Strategy Defined:**
- ✅ Testing pyramid (70% unit, 20% integration, 10% E2E)
- ✅ 90% code coverage target
- ✅ xUnit + Moq for testing
- ✅ Time acceleration for fast tests
- ✅ Hybrid concurrency testing (smoke + stress)
- ✅ Clear test organization (mirrors src/)
- ✅ Comprehensive test scenarios

**Next Step:** Implement the tests! 🧪

---

**All 12 Phases Complete! 🎉**

The Elevator Control System design and implementation is now complete. Ready for test implementation!
