# Unit Testing Plan - Elevator Control System

**Date:** January 23, 2026  
**Target Coverage:** 90% (70% unit, 20% integration, 10% E2E)  
**Framework:** xUnit with Moq for mocking  
**Estimated Effort:** 40-60 hours

---

## Table of Contents

1. [Testing Strategy Overview](#testing-strategy-overview)
2. [Test Pyramid Distribution](#test-pyramid-distribution)
3. [Unit Tests (70% - ~60 tests)](#unit-tests)
4. [Integration Tests (20% - ~15 tests)](#integration-tests)
5. [End-to-End Tests (10% - ~5 tests)](#end-to-end-tests)
6. [Test Infrastructure](#test-infrastructure)
7. [Priority Matrix](#priority-matrix)
8. [Implementation Roadmap](#implementation-roadmap)

---

## Testing Strategy Overview

### Goals
- ✅ Verify correctness of business logic
- ✅ Ensure thread safety and concurrency
- ✅ Validate error handling and edge cases
- ✅ Enable safe refactoring
- ✅ Document expected behavior

### Principles
1. **Arrange-Act-Assert (AAA)** pattern for all tests
2. **One assertion per test** (logical assertion, not physical)
3. **Fast execution** - Use mocks and time acceleration
4. **Deterministic** - No flaky tests, no Thread.Sleep()
5. **Readable** - Test names describe behavior

### Tools
- **xUnit** - Test framework
- **Moq** - Mocking library
- **FluentAssertions** - Readable assertions (optional)
- **MockTimeService** - Time acceleration for instant tests

---

## Test Pyramid Distribution

```
        /\
       /E2E\      10% - ~5 tests (Full system scenarios)
      /------\
     /  INT   \   20% - ~15 tests (Component interactions)
    /----------\
   /   UNIT     \ 70% - ~60 tests (Individual classes)
  /--------------\
```

### Why This Distribution?
- **Unit tests** are fast, isolated, and catch most bugs
- **Integration tests** verify component interactions
- **E2E tests** validate full system behavior

---

## Unit Tests

### Priority 1: Critical Business Logic (20 tests, 8-12 hours)

#### 1.1 Building Tests (8 tests, 3-4 hours)

**File:** `tests/ElevatorSystem.Tests/Domain/Entities/BuildingTests.cs`

##### Test: `RequestHallCall_ValidRequest_ReturnsSuccess`
```csharp
[Fact]
public void RequestHallCall_ValidRequest_ReturnsSuccess()
{
    // Arrange
    var building = CreateBuilding();
    
    // Act
    var result = building.RequestHallCall(5, Direction.UP, "TestSource");
    
    // Assert
    Assert.True(result.IsSuccess);
    Assert.NotNull(result.Value);
    Assert.Equal(5, result.Value.Floor);
    Assert.Equal(Direction.UP, result.Value.Direction);
}
```

##### Test: `RequestHallCall_FloorOutOfRange_ReturnsFailure`
```csharp
[Theory]
[InlineData(-1)]
[InlineData(11)]
[InlineData(100)]
public void RequestHallCall_FloorOutOfRange_ReturnsFailure(int invalidFloor)
{
    // Arrange
    var building = CreateBuilding(maxFloors: 10);
    
    // Act
    var result = building.RequestHallCall(invalidFloor, Direction.UP);
    
    // Assert
    Assert.False(result.IsSuccess);
    Assert.Contains("out of range", result.Error);
}
```

##### Test: `RequestHallCall_InvalidDirection_ReturnsFailure`
```csharp
[Fact]
public void RequestHallCall_InvalidDirection_ReturnsFailure()
{
    // Arrange
    var building = CreateBuilding();
    
    // Act
    var result = building.RequestHallCall(5, Direction.IDLE);
    
    // Assert
    Assert.False(result.IsSuccess);
    Assert.Contains("Invalid direction", result.Error);
}
```

##### Test: `RequestHallCall_DuplicateRequest_ReturnsExistingHallCall`
```csharp
[Fact]
public void RequestHallCall_DuplicateRequest_ReturnsExistingHallCall()
{
    // Arrange
    var building = CreateBuilding();
    var first = building.RequestHallCall(5, Direction.UP);
    
    // Act
    var second = building.RequestHallCall(5, Direction.UP);
    
    // Assert
    Assert.True(second.IsSuccess);
    Assert.Equal(first.Value.Id, second.Value.Id); // Same hall call
}
```

##### Test: `RequestHallCall_RateLimitExceeded_ReturnsFailure`
```csharp
[Fact]
public void RequestHallCall_RateLimitExceeded_ReturnsFailure()
{
    // Arrange
    var building = CreateBuilding();
    
    // Act - Make 11 requests (limit is 10 per source per minute)
    for (int i = 0; i < 10; i++)
    {
        building.RequestHallCall(i, Direction.UP, "TestSource");
    }
    var result = building.RequestHallCall(5, Direction.DOWN, "TestSource");
    
    // Assert
    Assert.False(result.IsSuccess);
    Assert.Contains("Rate limit exceeded", result.Error);
}
```

##### Test: `RequestHallCall_QueueAtCapacity_ReturnsFailure`
```csharp
[Fact]
public void RequestHallCall_QueueAtCapacity_ReturnsFailure()
{
    // Arrange
    var building = CreateBuilding(maxFloors: 10); // Max 18 hall calls
    
    // Act - Fill queue to capacity
    for (int floor = 1; floor < 10; floor++)
    {
        building.RequestHallCall(floor, Direction.UP, $"Source{floor}A");
        building.RequestHallCall(floor, Direction.DOWN, $"Source{floor}B");
    }
    var result = building.RequestHallCall(5, Direction.UP, "OverflowSource");
    
    // Assert
    Assert.False(result.IsSuccess);
    Assert.Contains("at capacity", result.Error);
}
```

##### Test: `ProcessTick_PendingHallCall_GetsAssigned`
```csharp
[Fact]
public void ProcessTick_PendingHallCall_GetsAssigned()
{
    // Arrange
    var building = CreateBuilding();
    var hallCall = building.RequestHallCall(5, Direction.UP).Value;
    
    // Act
    building.ProcessTick(); // Should assign to an elevator
    
    // Assert
    var status = building.GetStatus();
    Assert.Equal(HallCallStatus.ASSIGNED, hallCall.Status);
    Assert.True(status.Elevators.Any(e => e.Destinations.Contains(5)));
}
```

##### Test: `GetStatus_ReturnsCurrentState`
```csharp
[Fact]
public void GetStatus_ReturnsCurrentState()
{
    // Arrange
    var building = CreateBuilding(elevatorCount: 4, maxFloors: 10);
    building.RequestHallCall(5, Direction.UP);
    
    // Act
    var status = building.GetStatus();
    
    // Assert
    Assert.Equal(4, status.Elevators.Count);
    Assert.Equal(1, status.PendingHallCallsCount);
}
```

---

#### 1.2 Elevator Tests (12 tests, 5-6 hours)

**File:** `tests/ElevatorSystem.Tests/Domain/Entities/ElevatorTests.cs`

##### Test: `ProcessTick_IdleWithDestinations_TransitionsToMoving`
```csharp
[Fact]
public void ProcessTick_IdleWithDestinations_TransitionsToMoving()
{
    // Arrange
    var elevator = CreateElevator(id: 1, currentFloor: 0);
    elevator.AddDestination(5);
    
    // Act
    elevator.ProcessTick();
    
    // Assert
    Assert.Equal(ElevatorState.MOVING, elevator.State);
    Assert.Equal(Direction.UP, elevator.Direction);
}
```

##### Test: `ProcessTick_Moving_AdvancesOneFloor`
```csharp
[Theory]
[InlineData(0, 5, 1)] // Moving UP: 0 → 1
[InlineData(5, 0, 4)] // Moving DOWN: 5 → 4
public void ProcessTick_Moving_AdvancesOneFloor(int start, int destination, int expectedFloor)
{
    // Arrange
    var elevator = CreateElevator(currentFloor: start);
    elevator.AddDestination(destination);
    elevator.ProcessTick(); // Transition to MOVING
    
    // Act
    elevator.ProcessTick(); // Move one floor
    
    // Assert
    Assert.Equal(expectedFloor, elevator.CurrentFloor);
    Assert.Equal(ElevatorState.MOVING, elevator.State);
}
```

##### Test: `ProcessTick_ArrivesAtDestination_TransitionsToLoading`
```csharp
[Fact]
public void ProcessTick_ArrivesAtDestination_TransitionsToLoading()
{
    // Arrange
    var elevator = CreateElevator(currentFloor: 4);
    elevator.AddDestination(5);
    elevator.ProcessTick(); // IDLE → MOVING
    
    // Act
    elevator.ProcessTick(); // Move to floor 5
    
    // Assert
    Assert.Equal(5, elevator.CurrentFloor);
    Assert.Equal(ElevatorState.LOADING, elevator.State);
}
```

##### Test: `ProcessTick_LoadingDoorTimerExpires_TransitionsToIdle`
```csharp
[Fact]
public void ProcessTick_LoadingDoorTimerExpires_TransitionsToIdle()
{
    // Arrange
    var elevator = CreateElevator(currentFloor: 0, doorOpenTicks: 3);
    elevator.AddDestination(0); // Destination at current floor
    elevator.ProcessTick(); // IDLE → MOVING → LOADING
    
    // Act - Wait for door timer (3 ticks)
    elevator.ProcessTick(); // Timer: 2
    elevator.ProcessTick(); // Timer: 1
    elevator.ProcessTick(); // Timer: 0 → IDLE
    
    // Assert
    Assert.Equal(ElevatorState.IDLE, elevator.State);
}
```

##### Test: `ProcessTick_LoadingWithMoreDestinations_ContinuesMoving`
```csharp
[Fact]
public void ProcessTick_LoadingWithMoreDestinations_ContinuesMoving()
{
    // Arrange
    var elevator = CreateElevator(currentFloor: 0, doorOpenTicks: 1);
    elevator.AddDestination(3);
    elevator.AddDestination(5);
    elevator.ProcessTick(); // IDLE → MOVING
    elevator.ProcessTick(); // Move to 1
    elevator.ProcessTick(); // Move to 2
    elevator.ProcessTick(); // Move to 3 → LOADING
    
    // Act
    elevator.ProcessTick(); // Door timer expires
    
    // Assert
    Assert.Equal(ElevatorState.MOVING, elevator.State);
    Assert.Equal(Direction.UP, elevator.Direction);
}
```

##### Test: `ProcessTick_StuckInLoading_SafetyTimeoutForces Transition`
```csharp
[Fact]
public void ProcessTick_StuckInLoading_SafetyTimeoutForcesTransition()
{
    // Arrange
    var elevator = CreateElevator(currentFloor: 5, doorOpenTicks: 100); // Very long timer
    elevator.AddDestination(5);
    elevator.ProcessTick(); // IDLE → MOVING → LOADING
    
    // Act - Simulate 11 ticks (safety timeout is 10)
    for (int i = 0; i < 11; i++)
    {
        elevator.ProcessTick();
    }
    
    // Assert
    Assert.Equal(ElevatorState.IDLE, elevator.State); // Forced transition
}
```

##### Test: `CanAcceptHallCall_IdleElevator_ReturnsTrue`
```csharp
[Fact]
public void CanAcceptHallCall_IdleElevator_ReturnsTrue()
{
    // Arrange
    var elevator = CreateElevator(currentFloor: 3);
    var hallCall = new HallCall(7, Direction.UP);
    
    // Act
    var canAccept = elevator.CanAcceptHallCall(hallCall);
    
    // Assert
    Assert.True(canAccept);
}
```

##### Test: `CanAcceptHallCall_SameDirectionBetweenCurrentAndFurthest_ReturnsTrue`
```csharp
[Fact]
public void CanAcceptHallCall_SameDirectionBetweenCurrentAndFurthest_ReturnsTrue()
{
    // Arrange
    var elevator = CreateElevator(currentFloor: 2);
    elevator.AddDestination(8);
    elevator.ProcessTick(); // IDLE → MOVING UP
    var hallCall = new HallCall(5, Direction.UP); // Between 2 and 8
    
    // Act
    var canAccept = elevator.CanAcceptHallCall(hallCall);
    
    // Assert
    Assert.True(canAccept);
}
```

##### Test: `CanAcceptHallCall_OppositeDirection_ReturnsFalse`
```csharp
[Fact]
public void CanAcceptHallCall_OppositeDirection_ReturnsFalse()
{
    // Arrange
    var elevator = CreateElevator(currentFloor: 5);
    elevator.AddDestination(8);
    elevator.ProcessTick(); // IDLE → MOVING UP
    var hallCall = new HallCall(3, Direction.DOWN); // Opposite direction
    
    // Act
    var canAccept = elevator.CanAcceptHallCall(hallCall);
    
    // Assert
    Assert.False(canAccept);
}
```

##### Test: `CanAcceptHallCall_AtCurrentFloorInLoading_ReturnsFalse`
```csharp
[Fact]
public void CanAcceptHallCall_AtCurrentFloorInLoading_ReturnsFalse()
{
    // Arrange
    var elevator = CreateElevator(currentFloor: 5);
    elevator.AddDestination(5);
    elevator.ProcessTick(); // IDLE → MOVING → LOADING at floor 5
    var hallCall = new HallCall(5, Direction.UP);
    
    // Act
    var canAccept = elevator.CanAcceptHallCall(hallCall);
    
    // Assert
    Assert.False(canAccept); // Already servicing this floor
}
```

##### Test: `AssignHallCall_AddsDestinationAndHallCallId`
```csharp
[Fact]
public void AssignHallCall_AddsDestinationAndHallCallId()
{
    // Arrange
    var elevator = CreateElevator();
    var hallCall = new HallCall(7, Direction.UP);
    
    // Act
    elevator.AssignHallCall(hallCall);
    
    // Assert
    var status = elevator.GetStatus();
    Assert.Contains(7, status.Destinations);
    Assert.Contains(hallCall.Id, status.AssignedHallCallIds);
}
```

##### Test: `RemoveHallCallId_RemovesFromAssignedList`
```csharp
[Fact]
public void RemoveHallCallId_RemovesFromAssignedList()
{
    // Arrange
    var elevator = CreateElevator();
    var hallCall = new HallCall(7, Direction.UP);
    elevator.AssignHallCall(hallCall);
    
    // Act
    elevator.RemoveHallCallId(hallCall.Id);
    
    // Assert
    var status = elevator.GetStatus();
    Assert.DoesNotContain(hallCall.Id, status.AssignedHallCallIds);
}
```

---

### Priority 2: Value Objects & Domain Services (15 tests, 4-6 hours)

#### 2.1 DestinationSet Tests (6 tests, 2 hours)

**File:** `tests/ElevatorSystem.Tests/Domain/ValueObjects/DestinationSetTests.cs`

##### Test: `GetNextDestination_DirectionUp_ReturnsSmallestFloorAboveCurrent`
```csharp
[Fact]
public void GetNextDestination_DirectionUp_ReturnsSmallestFloorAboveCurrent()
{
    // Arrange
    var destinations = new DestinationSet(Direction.UP);
    destinations.Add(3);
    destinations.Add(7);
    destinations.Add(5);
    
    // Act
    var next = destinations.GetNextDestination(currentFloor: 4);
    
    // Assert
    Assert.Equal(5, next); // Smallest floor >= 4
}
```

##### Test: `GetNextDestination_DirectionDown_ReturnsLargestFloorBelowCurrent`
```csharp
[Fact]
public void GetNextDestination_DirectionDown_ReturnsLargestFloorBelowCurrent()
{
    // Arrange
    var destinations = new DestinationSet(Direction.DOWN);
    destinations.Add(3);
    destinations.Add(7);
    destinations.Add(5);
    
    // Act
    var next = destinations.GetNextDestination(currentFloor: 6);
    
    // Assert
    Assert.Equal(5, next); // Largest floor <= 6
}
```

##### Test: `GetNextDestination_FloorZeroIsValid_ReturnsZero`
```csharp
[Fact]
public void GetNextDestination_FloorZeroIsValid_ReturnsZero()
{
    // Arrange
    var destinations = new DestinationSet(Direction.DOWN);
    destinations.Add(0);
    destinations.Add(3);
    
    // Act
    var next = destinations.GetNextDestination(currentFloor: 2);
    
    // Assert
    Assert.Equal(0, next); // Floor 0 is valid!
}
```

##### Test: `GetNextDestination_DirectionIdle_ReturnsNearest`
```csharp
[Theory]
[InlineData(5, 5)] // Current at 5, nearest is 5
[InlineData(4, 5)] // Current at 4, nearest is 5
[InlineData(6, 5)] // Current at 6, nearest is 5
public void GetNextDestination_DirectionIdle_ReturnsNearest(int currentFloor, int expected)
{
    // Arrange
    var destinations = new DestinationSet(Direction.IDLE);
    destinations.Add(2);
    destinations.Add(5);
    destinations.Add(9);
    
    // Act
    var next = destinations.GetNextDestination(currentFloor);
    
    // Assert
    Assert.Equal(expected, next);
}
```

##### Test: `GetFurthestDestination_DirectionUp_ReturnsMax`
```csharp
[Fact]
public void GetFurthestDestination_DirectionUp_ReturnsMax()
{
    // Arrange
    var destinations = new DestinationSet(Direction.UP);
    destinations.Add(3);
    destinations.Add(9);
    destinations.Add(5);
    
    // Act
    var furthest = destinations.GetFurthestDestination();
    
    // Assert
    Assert.Equal(9, furthest);
}
```

##### Test: `Remove_RemovesDestination`
```csharp
[Fact]
public void Remove_RemovesDestination()
{
    // Arrange
    var destinations = new DestinationSet(Direction.UP);
    destinations.Add(5);
    destinations.Add(7);
    
    // Act
    destinations.Remove(5);
    
    // Assert
    Assert.False(destinations.Contains(5));
    Assert.True(destinations.Contains(7));
}
```

---

#### 2.2 DirectionAwareStrategy Tests (4 tests, 1.5 hours)

**File:** `tests/ElevatorSystem.Tests/Domain/Services/DirectionAwareStrategyTests.cs`

##### Test: `SelectBestElevator_PrioritizesSameDirection`
```csharp
[Fact]
public void SelectBestElevator_PrioritizesSameDirection()
{
    // Arrange
    var strategy = new DirectionAwareStrategy();
    var hallCall = new HallCall(5, Direction.UP);
    
    var elevator1 = CreateElevator(id: 1, currentFloor: 3); // Idle at 3
    var elevator2 = CreateElevator(id: 2, currentFloor: 2); // Moving UP
    elevator2.AddDestination(8);
    elevator2.ProcessTick(); // IDLE → MOVING UP
    
    var elevators = new List<Elevator> { elevator1, elevator2 };
    
    // Act
    var selected = strategy.SelectBestElevator(hallCall, elevators);
    
    // Assert
    Assert.Equal(2, selected.Id); // Elevator 2 (same direction) preferred
}
```

##### Test: `SelectBestElevator_PicksNearestWhenMultipleSameDirection`
```csharp
[Fact]
public void SelectBestElevator_PicksNearestWhenMultipleSameDirection()
{
    // Arrange
    var strategy = new DirectionAwareStrategy();
    var hallCall = new HallCall(5, Direction.UP);
    
    var elevator1 = CreateElevator(id: 1, currentFloor: 2);
    elevator1.AddDestination(8);
    elevator1.ProcessTick(); // Moving UP from 2
    
    var elevator2 = CreateElevator(id: 2, currentFloor: 4);
    elevator2.AddDestination(8);
    elevator2.ProcessTick(); // Moving UP from 4
    
    var elevators = new List<Elevator> { elevator1, elevator2 };
    
    // Act
    var selected = strategy.SelectBestElevator(hallCall, elevators);
    
    // Assert
    Assert.Equal(2, selected.Id); // Elevator 2 is nearer (floor 4 vs 2)
}
```

##### Test: `SelectBestElevator_FallbackToIdleWhenNoSameDirection`
```csharp
[Fact]
public void SelectBestElevator_FallbackToIdleWhenNoSameDirection()
{
    // Arrange
    var strategy = new DirectionAwareStrategy();
    var hallCall = new HallCall(5, Direction.UP);
    
    var elevator1 = CreateElevator(id: 1, currentFloor: 8);
    elevator1.AddDestination(2);
    elevator1.ProcessTick(); // Moving DOWN
    
    var elevator2 = CreateElevator(id: 2, currentFloor: 3); // Idle
    
    var elevators = new List<Elevator> { elevator1, elevator2 };
    
    // Act
    var selected = strategy.SelectBestElevator(hallCall, elevators);
    
    // Assert
    Assert.Equal(2, selected.Id); // Idle elevator selected
}
```

##### Test: `SelectBestElevator_NoElevatorsAvailable_ReturnsNull`
```csharp
[Fact]
public void SelectBestElevator_NoElevatorsAvailable_ReturnsNull()
{
    // Arrange
    var strategy = new DirectionAwareStrategy();
    var hallCall = new HallCall(5, Direction.UP);
    
    var elevator1 = CreateElevator(id: 1, currentFloor: 5);
    elevator1.AddDestination(5);
    elevator1.ProcessTick(); // LOADING at floor 5
    
    var elevators = new List<Elevator> { elevator1 };
    
    // Act
    var selected = strategy.SelectBestElevator(hallCall, elevators);
    
    // Assert
    Assert.Null(selected); // Cannot accept (already at floor 5 in LOADING)
}
```

---

#### 2.3 Value Object Tests (5 tests, 1.5 hours)

**File:** `tests/ElevatorSystem.Tests/Domain/ValueObjects/ValueObjectTests.cs`

##### Test: `Direction_Of_ValidValue_ReturnsCorrectInstance`
```csharp
[Theory]
[InlineData("UP")]
[InlineData("up")]
[InlineData("Up")]
public void Direction_Of_ValidValue_ReturnsCorrectInstance(string input)
{
    // Act
    var direction = Direction.Of(input);
    
    // Assert
    Assert.Equal(Direction.UP, direction);
}
```

##### Test: `Direction_Of_InvalidValue_ThrowsException`
```csharp
[Fact]
public void Direction_Of_InvalidValue_ThrowsException()
{
    // Act & Assert
    Assert.Throws<ArgumentException>(() => Direction.Of("LEFT"));
}
```

##### Test: `Journey_Of_ValidJourney_CreatesInstance`
```csharp
[Fact]
public void Journey_Of_ValidJourney_CreatesInstance()
{
    // Act
    var journey = Journey.Of(sourceFloor: 3, destinationFloor: 7);
    
    // Assert
    Assert.Equal(3, journey.SourceFloor);
    Assert.Equal(7, journey.DestinationFloor);
    Assert.Equal(Direction.UP, journey.Direction);
}
```

##### Test: `Journey_Of_SameFloor_ThrowsException`
```csharp
[Fact]
public void Journey_Of_SameFloor_ThrowsException()
{
    // Act & Assert
    var ex = Assert.Throws<ArgumentException>(() => Journey.Of(5, 5));
    Assert.Contains("cannot be the same", ex.Message);
}
```

##### Test: `Journey_Of_NegativeFloor_ThrowsException`
```csharp
[Theory]
[InlineData(-1, 5)]
[InlineData(5, -1)]
public void Journey_Of_NegativeFloor_ThrowsException(int source, int destination)
{
    // Act & Assert
    var ex = Assert.Throws<ArgumentException>(() => Journey.Of(source, destination));
    Assert.Contains("cannot be negative", ex.Message);
}
```

---

### Priority 3: Infrastructure & Common (10 tests, 3-4 hours)

#### 3.1 Result<T> Tests (3 tests, 1 hour)

**File:** `tests/ElevatorSystem.Tests/Common/ResultTests.cs`

##### Test: `Success_CreatesSuccessResult`
```csharp
[Fact]
public void Success_CreatesSuccessResult()
{
    // Act
    var result = Result<int>.Success(42);
    
    // Assert
    Assert.True(result.IsSuccess);
    Assert.Equal(42, result.Value);
    Assert.Null(result.Error);
}
```

##### Test: `Failure_CreatesFailureResult`
```csharp
[Fact]
public void Failure_CreatesFailureResult()
{
    // Act
    var result = Result<int>.Failure("Something went wrong");
    
    // Assert
    Assert.False(result.IsSuccess);
    Assert.Equal(0, result.Value); // Default value
    Assert.Equal("Something went wrong", result.Error);
}
```

##### Test: `Match_ExecutesCorrectBranch`
```csharp
[Fact]
public void Match_ExecutesCorrectBranch()
{
    // Arrange
    var success = Result<int>.Success(42);
    var failure = Result<int>.Failure("Error");
    
    // Act
    var successValue = success.Match(
        onSuccess: v => $"Value: {v}",
        onFailure: e => $"Error: {e}");
    
    var failureValue = failure.Match(
        onSuccess: v => $"Value: {v}",
        onFailure: e => $"Error: {e}");
    
    // Assert
    Assert.Equal("Value: 42", successValue);
    Assert.Equal("Error: Error", failureValue);
}
```

---

#### 3.2 RateLimiter Tests (4 tests, 1.5 hours)

**File:** `tests/ElevatorSystem.Tests/Common/RateLimiterTests.cs`

##### Test: `IsAllowed_WithinGlobalLimit_ReturnsTrue`
```csharp
[Fact]
public void IsAllowed_WithinGlobalLimit_ReturnsTrue()
{
    // Arrange
    var rateLimiter = CreateRateLimiter(globalLimit: 10, perSourceLimit: 5);
    
    // Act
    var result = rateLimiter.IsAllowed("Source1");
    
    // Assert
    Assert.True(result);
}
```

##### Test: `IsAllowed_ExceedsGlobalLimit_ReturnsFalse`
```csharp
[Fact]
public void IsAllowed_ExceedsGlobalLimit_ReturnsFalse()
{
    // Arrange
    var rateLimiter = CreateRateLimiter(globalLimit: 3, perSourceLimit: 10);
    
    // Act
    rateLimiter.IsAllowed("Source1");
    rateLimiter.IsAllowed("Source2");
    rateLimiter.IsAllowed("Source3");
    var result = rateLimiter.IsAllowed("Source4");
    
    // Assert
    Assert.False(result);
}
```

##### Test: `IsAllowed_ExceedsPerSourceLimit_ReturnsFalse`
```csharp
[Fact]
public void IsAllowed_ExceedsPerSourceLimit_ReturnsFalse()
{
    // Arrange
    var rateLimiter = CreateRateLimiter(globalLimit: 100, perSourceLimit: 2);
    
    // Act
    rateLimiter.IsAllowed("Source1");
    rateLimiter.IsAllowed("Source1");
    var result = rateLimiter.IsAllowed("Source1");
    
    // Assert
    Assert.False(result);
}
```

##### Test: `IsAllowed_AfterTimeWindow_AllowsNewRequests`
```csharp
[Fact]
public void IsAllowed_AfterTimeWindow_AllowsNewRequests()
{
    // Arrange
    var mockTime = new MockTimeService();
    var rateLimiter = CreateRateLimiter(globalLimit: 1, perSourceLimit: 1, timeService: mockTime);
    
    // Act
    rateLimiter.IsAllowed("Source1"); // First request
    mockTime.AdvanceTime(TimeSpan.FromMinutes(2)); // Move past 1-minute window
    var result = rateLimiter.IsAllowed("Source1"); // Should be allowed
    
    // Assert
    Assert.True(result);
}
```

---

#### 3.3 SystemMetrics Tests (3 tests, 1 hour)

**File:** `tests/ElevatorSystem.Tests/Infrastructure/Metrics/SystemMetricsTests.cs`

##### Test: `IncrementCounters_UpdatesSnapshot`
```csharp
[Fact]
public void IncrementCounters_UpdatesSnapshot()
{
    // Arrange
    var metrics = new SystemMetrics();
    
    // Act
    metrics.IncrementTotalRequests();
    metrics.IncrementAcceptedRequests();
    metrics.IncrementCompletedHallCalls();
    var snapshot = metrics.GetSnapshot();
    
    // Assert
    Assert.Equal(1, snapshot.TotalRequests);
    Assert.Equal(1, snapshot.AcceptedRequests);
    Assert.Equal(1, snapshot.CompletedHallCalls);
}
```

##### Test: `SetGauges_UpdatesSnapshot`
```csharp
[Fact]
public void SetGauges_UpdatesSnapshot()
{
    // Arrange
    var metrics = new SystemMetrics();
    
    // Act
    metrics.SetPendingHallCallsCount(5);
    metrics.SetActiveElevatorsCount(3);
    var snapshot = metrics.GetSnapshot();
    
    // Assert
    Assert.Equal(5, snapshot.PendingHallCalls);
    Assert.Equal(3, snapshot.ActiveElevators);
}
```

##### Test: `Metrics_ThreadSafe_ConcurrentIncrements`
```csharp
[Fact]
public void Metrics_ThreadSafe_ConcurrentIncrements()
{
    // Arrange
    var metrics = new SystemMetrics();
    var tasks = new List<Task>();
    
    // Act - 10 threads, each incrementing 100 times
    for (int i = 0; i < 10; i++)
    {
        tasks.Add(Task.Run(() =>
        {
            for (int j = 0; j < 100; j++)
            {
                metrics.IncrementTotalRequests();
            }
        }));
    }
    Task.WaitAll(tasks.ToArray());
    var snapshot = metrics.GetSnapshot();
    
    // Assert
    Assert.Equal(1000, snapshot.TotalRequests); // No lost increments
}
```

---

### Priority 4: Entity State Transitions (5 tests, 2 hours)

#### 4.1 HallCall Tests (3 tests, 1 hour)

**File:** `tests/ElevatorSystem.Tests/Domain/Entities/HallCallTests.cs`

##### Test: `MarkAsAssigned_FromPending_UpdatesStatus`
```csharp
[Fact]
public void MarkAsAssigned_FromPending_UpdatesStatus()
{
    // Arrange
    var hallCall = new HallCall(5, Direction.UP);
    
    // Act
    hallCall.MarkAsAssigned(elevatorId: 2);
    
    // Assert
    Assert.Equal(HallCallStatus.ASSIGNED, hallCall.Status);
    Assert.Equal(2, hallCall.AssignedElevatorId);
}
```

##### Test: `MarkAsAssigned_FromNonPending_ThrowsException`
```csharp
[Fact]
public void MarkAsAssigned_FromNonPending_ThrowsException()
{
    // Arrange
    var hallCall = new HallCall(5, Direction.UP);
    hallCall.MarkAsAssigned(1);
    
    // Act & Assert
    var ex = Assert.Throws<InvalidOperationException>(() => hallCall.MarkAsAssigned(2));
    Assert.Contains("Cannot assign", ex.Message);
}
```

##### Test: `MarkAsCompleted_FromAssigned_UpdatesStatus`
```csharp
[Fact]
public void MarkAsCompleted_FromAssigned_UpdatesStatus()
{
    // Arrange
    var hallCall = new HallCall(5, Direction.UP);
    hallCall.MarkAsAssigned(1);
    
    // Act
    hallCall.MarkAsCompleted();
    
    // Assert
    Assert.Equal(HallCallStatus.COMPLETED, hallCall.Status);
}
```

---

#### 4.2 Request Tests (2 tests, 1 hour)

**File:** `tests/ElevatorSystem.Tests/Domain/Entities/RequestTests.cs`

##### Test: `MarkAsInTransit_FromWaiting_UpdatesStatus`
```csharp
[Fact]
public void MarkAsInTransit_FromWaiting_UpdatesStatus()
{
    // Arrange
    var journey = Journey.Of(3, 7);
    var hallCallId = Guid.NewGuid();
    var request = new Request(hallCallId, journey);
    
    // Act
    request.MarkAsInTransit();
    
    // Assert
    Assert.Equal(RequestStatus.IN_TRANSIT, request.Status);
}
```

##### Test: `MarkAsCompleted_FromInTransit_UpdatesStatus`
```csharp
[Fact]
public void MarkAsCompleted_FromInTransit_UpdatesStatus()
{
    // Arrange
    var journey = Journey.Of(3, 7);
    var request = new Request(Guid.NewGuid(), journey);
    request.MarkAsInTransit();
    
    // Act
    request.MarkAsCompleted();
    
    // Assert
    Assert.Equal(RequestStatus.COMPLETED, request.Status);
}
```

---

### Priority 5: Configuration & Validation (5 tests, 2 hours)

#### 5.1 ConfigurationLoader Tests (5 tests, 2 hours)

**File:** `tests/ElevatorSystem.Tests/Infrastructure/Configuration/ConfigurationLoaderTests.cs`

##### Test: `Load_ValidConfiguration_ReturnsConfig`
```csharp
[Fact]
public void Load_ValidConfiguration_ReturnsConfig()
{
    // Arrange
    var json = @"{
        ""MaxFloors"": 15,
        ""ElevatorCount"": 6,
        ""TickIntervalMs"": 500,
        ""DoorOpenTicks"": 2,
        ""RequestIntervalSeconds"": 10
    }";
    File.WriteAllText("test-config.json", json);
    
    // Act
    var config = ConfigurationLoader.Load("test-config.json");
    
    // Assert
    Assert.Equal(15, config.MaxFloors);
    Assert.Equal(6, config.ElevatorCount);
    
    // Cleanup
    File.Delete("test-config.json");
}
```

##### Test: `Load_MissingFile_ReturnsDefaults`
```csharp
[Fact]
public void Load_MissingFile_ReturnsDefaults()
{
    // Act
    var config = ConfigurationLoader.Load("nonexistent.json");
    
    // Assert
    Assert.Equal(10, config.MaxFloors); // Default value
    Assert.Equal(4, config.ElevatorCount); // Default value
}
```

##### Test: `Validate_InvalidMaxFloors_ThrowsException`
```csharp
[Theory]
[InlineData(1)]   // Too low
[InlineData(101)] // Too high
public void Validate_InvalidMaxFloors_ThrowsException(int invalidFloors)
{
    // Arrange
    var config = new SimulationConfiguration { MaxFloors = invalidFloors };
    
    // Act & Assert
    Assert.Throws<ArgumentException>(() => ConfigurationLoader.Validate(config));
}
```

##### Test: `Validate_InvalidElevatorCount_ThrowsException`
```csharp
[Theory]
[InlineData(0)]  // Too low
[InlineData(11)] // Too high
public void Validate_InvalidElevatorCount_ThrowsException(int invalidCount)
{
    // Arrange
    var config = new SimulationConfiguration { ElevatorCount = invalidCount };
    
    // Act & Assert
    Assert.Throws<ArgumentException>(() => ConfigurationLoader.Validate(config));
}
```

##### Test: `Validate_ValidConfiguration_DoesNotThrow`
```csharp
[Fact]
public void Validate_ValidConfiguration_DoesNotThrow()
{
    // Arrange
    var config = SimulationConfiguration.Default();
    
    // Act & Assert
    var exception = Record.Exception(() => ConfigurationLoader.Validate(config));
    Assert.Null(exception);
}
```

---

## Integration Tests

### Priority 1: Component Interactions (15 tests, 8-10 hours)

#### INT-1: Full Request Lifecycle (3 tests, 2 hours)

**File:** `tests/ElevatorSystem.Tests/Integration/RequestLifecycleTests.cs`

##### Test: `RequestLifecycle_HallCallToCompletion_FullFlow`
```csharp
[Fact]
public void RequestLifecycle_HallCallToCompletion_FullFlow()
{
    // Arrange
    var building = CreateBuilding(elevatorCount: 1, maxFloors: 10);
    
    // Act
    var result = building.RequestHallCall(5, Direction.UP);
    Assert.True(result.IsSuccess);
    
    // Process ticks until hall call is completed
    for (int i = 0; i < 20; i++)
    {
        building.ProcessTick();
        if (result.Value.Status == HallCallStatus.COMPLETED)
            break;
    }
    
    // Assert
    Assert.Equal(HallCallStatus.COMPLETED, result.Value.Status);
}
```

##### Test: `MultipleHallCalls_ProcessedInFIFOOrder`
```csharp
[Fact]
public void MultipleHallCalls_ProcessedInFIFOOrder()
{
    // Arrange
    var building = CreateBuilding(elevatorCount: 1, maxFloors: 10);
    
    // Act - Create 3 hall calls
    var call1 = building.RequestHallCall(3, Direction.UP).Value;
    Thread.Sleep(10); // Ensure different timestamps
    var call2 = building.RequestHallCall(7, Direction.UP).Value;
    Thread.Sleep(10);
    var call3 = building.RequestHallCall(5, Direction.UP).Value;
    
    // Process until first is assigned
    building.ProcessTick();
    
    // Assert - First call should be assigned first
    Assert.Equal(HallCallStatus.ASSIGNED, call1.Status);
    Assert.Equal(HallCallStatus.PENDING, call2.Status);
    Assert.Equal(HallCallStatus.PENDING, call3.Status);
}
```

##### Test: `ElevatorServesMultipleFloorsInOneTrip`
```csharp
[Fact]
public void ElevatorServesMultipleFloorsInOneTrip()
{
    // Arrange
    var building = CreateBuilding(elevatorCount: 1, maxFloors: 10);
    
    // Act - Create hall calls at floors 3, 5, 7 (all UP)
    var call1 = building.RequestHallCall(3, Direction.UP).Value;
    var call2 = building.RequestHallCall(5, Direction.UP).Value;
    var call3 = building.RequestHallCall(7, Direction.UP).Value;
    
    // Process ticks until all completed
    for (int i = 0; i < 50; i++)
    {
        building.ProcessTick();
    }
    
    // Assert - All should be completed
    Assert.Equal(HallCallStatus.COMPLETED, call1.Status);
    Assert.Equal(HallCallStatus.COMPLETED, call2.Status);
    Assert.Equal(HallCallStatus.COMPLETED, call3.Status);
}
```

---

#### INT-2: Concurrency Tests (5 tests, 3 hours)

**File:** `tests/ElevatorSystem.Tests/Integration/ConcurrencyTests.cs`

##### Test: `ConcurrentRequests_NoRaceConditions`
```csharp
[Fact]
public void ConcurrentRequests_NoRaceConditions()
{
    // Arrange
    var building = CreateBuilding(elevatorCount: 4, maxFloors: 10);
    var tasks = new List<Task<Result<HallCall>>>();
    
    // Act - 100 threads making concurrent requests
    for (int i = 0; i < 100; i++)
    {
        int floor = i % 10;
        var direction = (i % 2 == 0) ? Direction.UP : Direction.DOWN;
        tasks.Add(Task.Run(() => building.RequestHallCall(floor, direction, $"Source{i}")));
    }
    Task.WaitAll(tasks.ToArray());
    
    // Assert - No exceptions, all requests processed
    var successCount = tasks.Count(t => t.Result.IsSuccess);
    Assert.True(successCount > 0); // At least some succeeded
}
```

##### Test: `ConcurrentTicks_ThreadSafe`
```csharp
[Fact]
public void ConcurrentTicks_ThreadSafe()
{
    // Arrange
    var building = CreateBuilding();
    building.RequestHallCall(5, Direction.UP);
    
    // Act - Multiple threads calling ProcessTick
    var tasks = new List<Task>();
    for (int i = 0; i < 10; i++)
    {
        tasks.Add(Task.Run(() =>
        {
            for (int j = 0; j < 10; j++)
            {
                building.ProcessTick();
            }
        }));
    }
    
    // Assert - No exceptions thrown
    var exception = Record.Exception(() => Task.WaitAll(tasks.ToArray()));
    Assert.Null(exception);
}
```

##### Test: `ConcurrentStatusQueries_DoNotBlock`
```csharp
[Fact]
public void ConcurrentStatusQueries_DoNotBlock()
{
    // Arrange
    var building = CreateBuilding();
    var stopwatch = Stopwatch.StartNew();
    
    // Act - 1000 concurrent status queries
    var tasks = new List<Task<BuildingStatus>>();
    for (int i = 0; i < 1000; i++)
    {
        tasks.Add(Task.Run(() => building.GetStatus()));
    }
    Task.WaitAll(tasks.ToArray());
    stopwatch.Stop();
    
    // Assert - Should complete quickly (< 1 second)
    Assert.True(stopwatch.ElapsedMilliseconds < 1000);
}
```

##### Test: `RateLimiter_ThreadSafe_ConcurrentChecks`
```csharp
[Fact]
public void RateLimiter_ThreadSafe_ConcurrentChecks()
{
    // Arrange
    var rateLimiter = CreateRateLimiter(globalLimit: 100, perSourceLimit: 50);
    var allowedCount = 0;
    var lockObj = new object();
    
    // Act - 100 threads checking rate limit
    var tasks = new List<Task>();
    for (int i = 0; i < 100; i++)
    {
        tasks.Add(Task.Run(() =>
        {
            if (rateLimiter.IsAllowed("TestSource"))
            {
                lock (lockObj) { allowedCount++; }
            }
        }));
    }
    Task.WaitAll(tasks.ToArray());
    
    // Assert - Should respect per-source limit (50)
    Assert.True(allowedCount <= 50);
}
```

##### Test: `Metrics_ThreadSafe_ConcurrentUpdates`
```csharp
[Fact]
public void Metrics_ThreadSafe_ConcurrentUpdates()
{
    // Arrange
    var metrics = new SystemMetrics();
    
    // Act - 10 threads, each incrementing 1000 times
    var tasks = new List<Task>();
    for (int i = 0; i < 10; i++)
    {
        tasks.Add(Task.Run(() =>
        {
            for (int j = 0; j < 1000; j++)
            {
                metrics.IncrementTotalRequests();
                metrics.IncrementAcceptedRequests();
            }
        }));
    }
    Task.WaitAll(tasks.ToArray());
    var snapshot = metrics.GetSnapshot();
    
    // Assert - No lost updates
    Assert.Equal(10000, snapshot.TotalRequests);
    Assert.Equal(10000, snapshot.AcceptedRequests);
}
```

---

#### INT-3: Scheduling Algorithm Integration (4 tests, 2 hours)

**File:** `tests/ElevatorSystem.Tests/Integration/SchedulingIntegrationTests.cs`

##### Test: `DirectionAwareStrategy_AssignsElevatorMovingInSameDirection`
```csharp
[Fact]
public void DirectionAwareStrategy_AssignsElevatorMovingInSameDirection()
{
    // Arrange
    var building = CreateBuilding(elevatorCount: 2, maxFloors: 10);
    
    // Elevator 1: Moving UP from 2 to 8
    building.RequestHallCall(8, Direction.UP);
    building.ProcessTick(); // Assigns to Elevator 1
    
    // Act - Request at floor 5 (UP) - should go to Elevator 1
    var result = building.RequestHallCall(5, Direction.UP);
    building.ProcessTick();
    
    // Assert
    var status = building.GetStatus();
    var elevator1 = status.Elevators.First(e => e.Id == 1);
    Assert.Contains(5, elevator1.Destinations);
}
```

##### Test: `NearestFirstStrategy_AssignsNearestElevator`
```csharp
[Fact]
public void NearestFirstStrategy_AssignsNearestElevator()
{
    // Arrange
    var strategy = new NearestFirstStrategy();
    var building = CreateBuilding(strategy, elevatorCount: 3, maxFloors: 10);
    
    // Position elevators at different floors
    // (This requires a way to set initial positions - may need test helper)
    
    // Act
    var result = building.RequestHallCall(5, Direction.UP);
    building.ProcessTick();
    
    // Assert - Nearest elevator should be assigned
    Assert.True(result.IsSuccess);
    Assert.Equal(HallCallStatus.ASSIGNED, result.Value.Status);
}
```

##### Test: `MultipleElevators_DistributeLoad`
```csharp
[Fact]
public void MultipleElevators_DistributeLoad()
{
    // Arrange
    var building = CreateBuilding(elevatorCount: 4, maxFloors: 10);
    
    // Act - Create 8 hall calls
    for (int i = 1; i <= 8; i++)
    {
        building.RequestHallCall(i, Direction.UP, $"Source{i}");
    }
    
    // Process ticks to assign all
    for (int i = 0; i < 5; i++)
    {
        building.ProcessTick();
    }
    
    // Assert - Load should be distributed across elevators
    var status = building.GetStatus();
    var activeElevators = status.Elevators.Count(e => e.State != ElevatorState.IDLE);
    Assert.True(activeElevators >= 2); // At least 2 elevators active
}
```

##### Test: `NoElevatorAvailable_HallCallStaysPending`
```csharp
[Fact]
public void NoElevatorAvailable_HallCallStaysPending()
{
    // Arrange
    var building = CreateBuilding(elevatorCount: 1, maxFloors: 10);
    
    // Occupy the only elevator
    building.RequestHallCall(9, Direction.UP);
    building.ProcessTick(); // Assigns to Elevator 1
    
    // Act - Request in opposite direction
    var result = building.RequestHallCall(2, Direction.DOWN);
    building.ProcessTick();
    
    // Assert - Should stay PENDING (elevator moving UP, can't accept DOWN)
    Assert.Equal(HallCallStatus.PENDING, result.Value.Status);
}
```

---

#### INT-4: Error Handling Integration (3 tests, 1.5 hours)

**File:** `tests/ElevatorSystem.Tests/Integration/ErrorHandlingTests.cs`

##### Test: `InvalidRequest_ReturnsFailureWithoutCrashing`
```csharp
[Fact]
public void InvalidRequest_ReturnsFailureWithoutCrashing()
{
    // Arrange
    var building = CreateBuilding();
    
    // Act
    var result1 = building.RequestHallCall(-1, Direction.UP);
    var result2 = building.RequestHallCall(100, Direction.UP);
    var result3 = building.RequestHallCall(5, Direction.IDLE);
    
    // Assert
    Assert.False(result1.IsSuccess);
    Assert.False(result2.IsSuccess);
    Assert.False(result3.IsSuccess);
    
    // System still functional
    var validResult = building.RequestHallCall(5, Direction.UP);
    Assert.True(validResult.IsSuccess);
}
```

##### Test: `RateLimitExceeded_RejectsRequestGracefully`
```csharp
[Fact]
public void RateLimitExceeded_RejectsRequestGracefully()
{
    // Arrange
    var building = CreateBuilding();
    
    // Act - Exceed per-source limit (10 requests)
    for (int i = 0; i < 10; i++)
    {
        building.RequestHallCall(i, Direction.UP, "TestSource");
    }
    var result = building.RequestHallCall(5, Direction.DOWN, "TestSource");
    
    // Assert
    Assert.False(result.IsSuccess);
    Assert.Contains("Rate limit", result.Error);
    
    // Different source should still work
    var otherResult = building.RequestHallCall(5, Direction.DOWN, "OtherSource");
    Assert.True(otherResult.IsSuccess);
}
```

##### Test: `QueueFull_RejectsNewRequests`
```csharp
[Fact]
public void QueueFull_RejectsNewRequests()
{
    // Arrange
    var building = CreateBuilding(maxFloors: 10); // Max 18 hall calls
    
    // Act - Fill queue
    for (int floor = 1; floor < 10; floor++)
    {
        building.RequestHallCall(floor, Direction.UP, $"SourceA{floor}");
        building.RequestHallCall(floor, Direction.DOWN, $"SourceB{floor}");
    }
    var result = building.RequestHallCall(5, Direction.UP, "OverflowSource");
    
    // Assert
    Assert.False(result.IsSuccess);
    Assert.Contains("capacity", result.Error);
}
```

---

## End-to-End Tests

### Priority 1: Full System Scenarios (5 tests, 4-5 hours)

#### E2E-1: Complete Simulation Scenarios (5 tests, 4-5 hours)

**File:** `tests/ElevatorSystem.Tests/E2E/SimulationScenarios Tests.cs`

##### Test: `Scenario_RushHour_HandlesHighLoad`
```csharp
[Fact]
public void Scenario_RushHour_HandlesHighLoad()
{
    // Arrange
    var building = CreateBuilding(elevatorCount: 4, maxFloors: 20);
    var completedCount = 0;
    
    // Act - Simulate rush hour: 50 requests over 30 seconds
    for (int i = 0; i < 50; i++)
    {
        var floor = Random.Shared.Next(1, 20);
        var direction = (i % 2 == 0) ? Direction.UP : Direction.DOWN;
        var result = building.RequestHallCall(floor, direction, $"Passenger{i}");
        
        if (result.IsSuccess)
        {
            completedCount++;
        }
        
        // Process ticks
        for (int tick = 0; tick < 5; tick++)
        {
            building.ProcessTick();
        }
    }
    
    // Assert - Most requests should be accepted
    Assert.True(completedCount >= 40); // At least 80% accepted
}
```

##### Test: `Scenario_SingleElevator_ServesAllFloors`
```csharp
[Fact]
public void Scenario_SingleElevator_ServesAllFloors()
{
    // Arrange
    var building = CreateBuilding(elevatorCount: 1, maxFloors: 10);
    
    // Act - Request all floors
    var calls = new List<HallCall>();
    for (int floor = 1; floor <= 9; floor++)
    {
        var result = building.RequestHallCall(floor, Direction.UP, $"Source{floor}");
        if (result.IsSuccess)
            calls.Add(result.Value);
    }
    
    // Process until all completed (max 200 ticks)
    for (int i = 0; i < 200; i++)
    {
        building.ProcessTick();
        if (calls.All(c => c.Status == HallCallStatus.COMPLETED))
            break;
    }
    
    // Assert - All calls completed
    Assert.All(calls, c => Assert.Equal(HallCallStatus.COMPLETED, c.Status));
}
```

##### Test: `Scenario_OppositeDirections_EfficientScheduling`
```csharp
[Fact]
public void Scenario_OppositeDirections_EfficientScheduling()
{
    // Arrange
    var building = CreateBuilding(elevatorCount: 2, maxFloors: 10);
    
    // Act - Create UP and DOWN requests
    var upCalls = new List<HallCall>();
    var downCalls = new List<HallCall>();
    
    for (int floor = 1; floor <= 5; floor++)
    {
        var upResult = building.RequestHallCall(floor, Direction.UP, $"Up{floor}");
        if (upResult.IsSuccess) upCalls.Add(upResult.Value);
        
        var downResult = building.RequestHallCall(floor + 5, Direction.DOWN, $"Down{floor}");
        if (downResult.IsSuccess) downCalls.Add(downResult.Value);
    }
    
    // Process ticks
    for (int i = 0; i < 100; i++)
    {
        building.ProcessTick();
    }
    
    // Assert - All completed
    Assert.All(upCalls, c => Assert.Equal(HallCallStatus.COMPLETED, c.Status));
    Assert.All(downCalls, c => Assert.Equal(HallCallStatus.COMPLETED, c.Status));
}
```

##### Test: `Scenario_GracefulDegradation_OneElevatorStuck`
```csharp
[Fact]
public void Scenario_GracefulDegradation_OneElevatorStuck()
{
    // Arrange
    var building = CreateBuilding(elevatorCount: 3, maxFloors: 10);
    
    // Act - Simulate one elevator getting stuck (very long door timer)
    // (This requires a way to inject a stuck elevator - may need test helper)
    
    // Create requests
    var calls = new List<HallCall>();
    for (int i = 1; i <= 5; i++)
    {
        var result = building.RequestHallCall(i, Direction.UP, $"Source{i}");
        if (result.IsSuccess) calls.Add(result.Value);
    }
    
    // Process ticks
    for (int i = 0; i < 100; i++)
    {
        building.ProcessTick();
    }
    
    // Assert - System continues with remaining elevators
    var completedCount = calls.Count(c => c.Status == HallCallStatus.COMPLETED);
    Assert.True(completedCount >= 3); // At least some completed
}
```

##### Test: `Scenario_MixedLoad_RealisticBehavior`
```csharp
[Fact]
public void Scenario_MixedLoad_RealisticBehavior()
{
    // Arrange
    var mockTime = new MockTimeService();
    var building = CreateBuilding(elevatorCount: 4, maxFloors: 15, timeService: mockTime);
    var random = new Random(42); // Seeded for reproducibility
    
    // Act - Simulate 2 minutes of mixed traffic
    var allCalls = new List<HallCall>();
    
    for (int second = 0; second < 120; second++)
    {
        // Random request every 3-7 seconds
        if (second % random.Next(3, 8) == 0)
        {
            var floor = random.Next(1, 15);
            var direction = random.Next(2) == 0 ? Direction.UP : Direction.DOWN;
            var result = building.RequestHallCall(floor, direction, $"Passenger{second}");
            if (result.IsSuccess)
                allCalls.Add(result.Value);
        }
        
        // Process tick
        building.ProcessTick();
        mockTime.AdvanceTime(TimeSpan.FromSeconds(1));
    }
    
    // Assert - System handled load efficiently
    var completedCount = allCalls.Count(c => c.Status == HallCallStatus.COMPLETED);
    var acceptanceRate = (double)allCalls.Count / 40; // ~40 requests expected
    
    Assert.True(acceptanceRate > 0.8); // >80% acceptance rate
    Assert.True(completedCount > allCalls.Count * 0.7); // >70% completed
}
```

---

## Test Infrastructure

### Test Helpers & Utilities

#### File: `tests/ElevatorSystem.Tests/TestHelpers/TestBuilders.cs`

```csharp
public static class TestBuilders
{
    public static Building CreateBuilding(
        ISchedulingStrategy? strategy = null,
        int elevatorCount = 4,
        int maxFloors = 10,
        int doorOpenTicks = 3,
        ITimeService? timeService = null)
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
    
    public static RateLimiter CreateRateLimiter(
        int globalLimit = 20,
        int perSourceLimit = 10,
        ITimeService? timeService = null)
    {
        var logger = new MockLogger();
        return new RateLimiter(globalLimit, perSourceLimit, logger);
    }
}
```

---

#### File: `tests/ElevatorSystem.Tests/TestHelpers/MockLogger.cs`

```csharp
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
```

---

#### File: `tests/ElevatorSystem.Tests/TestHelpers/MockTimeService.cs`

```csharp
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
```

---

## Priority Matrix

| Priority | Category | Tests | Effort | Business Value | Risk Mitigation |
|----------|----------|-------|--------|----------------|-----------------|
| **P1** | Building | 8 | 3-4h | ⭐⭐⭐⭐⭐ | Critical business logic |
| **P1** | Elevator | 12 | 5-6h | ⭐⭐⭐⭐⭐ | State machine correctness |
| **P2** | DestinationSet | 6 | 2h | ⭐⭐⭐⭐ | Algorithm correctness |
| **P2** | Scheduling | 4 | 1.5h | ⭐⭐⭐⭐ | Efficiency |
| **P2** | Value Objects | 5 | 1.5h | ⭐⭐⭐ | Input validation |
| **P3** | Result<T> | 3 | 1h | ⭐⭐⭐ | Error handling pattern |
| **P3** | RateLimiter | 4 | 1.5h | ⭐⭐⭐⭐ | DoS protection |
| **P3** | Metrics | 3 | 1h | ⭐⭐⭐ | Observability |
| **P4** | HallCall | 3 | 1h | ⭐⭐⭐ | State transitions |
| **P4** | Request | 2 | 1h | ⭐⭐ | State transitions |
| **P5** | Configuration | 5 | 2h | ⭐⭐⭐ | Fail-fast validation |
| **INT** | Integration | 15 | 8-10h | ⭐⭐⭐⭐⭐ | Component interactions |
| **E2E** | End-to-End | 5 | 4-5h | ⭐⭐⭐⭐⭐ | Full system validation |

**Total: ~85 tests, 40-60 hours**

---

## Implementation Roadmap

### Phase 1: Foundation (Week 1, 16-20 hours)
**Goal:** Test infrastructure + critical business logic

1. **Day 1-2: Setup (4 hours)**
   - Create test project structure
   - Add xUnit, Moq, FluentAssertions packages
   - Implement MockLogger, MockTimeService
   - Implement TestBuilders helpers

2. **Day 3-4: Building Tests (6 hours)**
   - Implement 8 Building tests (P1)
   - Focus on request validation, rate limiting, queue capacity

3. **Day 5-7: Elevator Tests (8 hours)**
   - Implement 12 Elevator tests (P1)
   - Focus on state machine transitions

**Deliverable:** 20 tests, critical business logic covered

---

### Phase 2: Domain Logic (Week 2, 12-16 hours)
**Goal:** Value objects + scheduling algorithms

1. **Day 8-9: DestinationSet Tests (4 hours)**
   - Implement 6 DestinationSet tests (P2)
   - Test floor 0 edge case

2. **Day 10: Scheduling Tests (3 hours)**
   - Implement 4 DirectionAwareStrategy tests (P2)

3. **Day 11: Value Objects (3 hours)**
   - Implement 5 value object tests (P2)

4. **Day 12: Entity State Transitions (3 hours)**
   - Implement 5 HallCall + Request tests (P4)

**Deliverable:** 40 tests total, domain logic fully covered

---

### Phase 3: Infrastructure (Week 3, 8-10 hours)
**Goal:** Common utilities + infrastructure

1. **Day 13: Result<T> + RateLimiter (4 hours)**
   - Implement 7 tests (P3)

2. **Day 14: Metrics + Configuration (4 hours)**
   - Implement 8 tests (P3, P5)

**Deliverable:** 55 tests total, infrastructure covered

---

### Phase 4: Integration (Week 4, 12-14 hours)
**Goal:** Component interactions + concurrency

1. **Day 15-16: Request Lifecycle (4 hours)**
   - Implement 3 integration tests (INT-1)

2. **Day 17-18: Concurrency Tests (5 hours)**
   - Implement 5 concurrency tests (INT-2)

3. **Day 19: Scheduling Integration (3 hours)**
   - Implement 4 scheduling integration tests (INT-3)

4. **Day 20: Error Handling (2 hours)**
   - Implement 3 error handling tests (INT-4)

**Deliverable:** 70 tests total, integration covered

---

### Phase 5: End-to-End (Week 5, 8-10 hours)
**Goal:** Full system scenarios

1. **Day 21-23: E2E Scenarios (8 hours)**
   - Implement 5 E2E tests
   - Rush hour, single elevator, opposite directions, degradation, mixed load

2. **Day 24: Polish & Documentation (2 hours)**
   - Review all tests
   - Update documentation
   - Generate coverage report

**Deliverable:** 85 tests total, 90% coverage achieved

---

## Running Tests

### Commands

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# Run specific category
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"
dotnet test --filter "Category=E2E"

# Run specific test
dotnet test --filter "FullyQualifiedName~BuildingTests.RequestHallCall_ValidRequest"

# Run in parallel
dotnet test --parallel

# Generate coverage report
dotnet test /p:CollectCoverage=true /p:CoverletOutput=./coverage/ /p:CoverletOutputFormat=html
```

---

### Test Categories

Use `[Trait("Category", "...")]` to categorize tests:

```csharp
[Fact]
[Trait("Category", "Unit")]
public void Test_Unit() { }

[Fact]
[Trait("Category", "Integration")]
public void Test_Integration() { }

[Fact]
[Trait("Category", "E2E")]
public void Test_E2E() { }

[Fact]
[Trait("Category", "Concurrency")]
public void Test_Concurrency() { }
```

---

## Coverage Goals

### Target: 90% Overall

| Layer | Target | Priority |
|-------|--------|----------|
| Domain/Entities | 95% | Critical |
| Domain/ValueObjects | 95% | Critical |
| Domain/Services | 90% | High |
| Common | 90% | High |
| Infrastructure | 80% | Medium |
| Application | 70% | Medium |

### Exclusions
- Program.cs (entry point)
- ConsoleLogger (output only)
- SystemTimeService (thin wrapper)

---

## Success Criteria

✅ **85+ tests implemented**  
✅ **90% code coverage achieved**  
✅ **All tests pass consistently (no flaky tests)**  
✅ **Test execution < 30 seconds**  
✅ **Zero race conditions detected**  
✅ **All critical paths covered**  
✅ **Edge cases tested (floor 0, rate limits, queue full)**  
✅ **Concurrency validated (100+ thread stress tests)**  

---

## Maintenance

### Adding New Tests
1. Follow AAA pattern (Arrange-Act-Assert)
2. One logical assertion per test
3. Descriptive test names: `MethodName_Scenario_ExpectedBehavior`
4. Use test helpers to reduce boilerplate
5. Add `[Trait]` for categorization

### Test Smells to Avoid
❌ **Thread.Sleep()** - Use MockTimeService instead  
❌ **Random values** - Use seeded Random for reproducibility  
❌ **External dependencies** - Mock all I/O  
❌ **Test interdependence** - Each test should be independent  
❌ **Magic numbers** - Use constants or variables  

---

## Conclusion

This comprehensive testing plan provides:
- **85 tests** covering unit, integration, and E2E scenarios
- **40-60 hour** implementation roadmap across 5 weeks
- **90% coverage** target with focus on critical business logic
- **Test infrastructure** (mocks, builders, helpers)
- **Concurrency validation** to ensure thread safety
- **Clear priorities** to guide implementation order

**Next Steps:**
1. Review and approve this plan
2. Create test project structure
3. Implement Phase 1 (Foundation) tests
4. Iterate through remaining phases

**Estimated Timeline:** 5 weeks part-time or 2 weeks full-time

---

**Document Version:** 1.0  
**Last Updated:** January 23, 2026  
**Author:** Senior Java Engineer & Interview Coach
