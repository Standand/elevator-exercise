# Integration Test Plan

**Date:** January 23, 2026  
**Status:** Planning Phase  
**Target:** Comprehensive integration test coverage

---

## Overview

Integration tests verify the interaction between multiple components working together. Unlike unit tests that test individual components in isolation, integration tests validate that components collaborate correctly to achieve business goals.

---

## Test Categories

### 1. Building + Elevator + Scheduling Strategy Integration (8 tests)

#### 1.1 Hall Call Assignment Flow
- **Test**: `RequestHallCall_WithIdleElevator_AssignsToNearestElevator`
  - **Scenario**: Request hall call when idle elevator exists
  - **Verify**: Hall call assigned to correct elevator, elevator transitions to MOVING

- **Test**: `RequestHallCall_WithMovingElevator_AssignsToSameDirectionElevator`
  - **Scenario**: Request hall call when elevator moving in same direction
  - **Verify**: Hall call assigned to moving elevator, destination added

- **Test**: `RequestHallCall_NoAvailableElevator_RemainsPending`
  - **Scenario**: Request hall call when no elevator can accept
  - **Verify**: Hall call remains PENDING, retried on next tick

- **Test**: `RequestHallCall_MultipleElevators_SelectsBestElevator`
  - **Scenario**: Multiple elevators available, different states
  - **Verify**: Scheduling strategy selects optimal elevator

#### 1.2 Direction-Aware Scheduling
- **Test**: `DirectionAwareStrategy_PrioritizesSameDirectionElevator`
  - **Scenario**: UP hall call, one elevator going UP, one IDLE
  - **Verify**: UP elevator selected over IDLE

- **Test**: `DirectionAwareStrategy_FallbackToIdleWhenNoSameDirection`
  - **Scenario**: UP hall call, all elevators going DOWN or IDLE
  - **Verify**: IDLE elevator selected

- **Test**: `DirectionAwareStrategy_SelectsNearestAmongSameDirection`
  - **Scenario**: Multiple elevators moving in same direction
  - **Verify**: Nearest elevator selected

#### 1.3 Nearest-First Scheduling
- **Test**: `NearestFirstStrategy_SelectsNearestIdleElevator`
  - **Scenario**: Multiple idle elevators at different floors
  - **Verify**: Nearest elevator selected regardless of direction

---

### 2. Complete Request Lifecycle Integration (6 tests)

#### 2.1 Full Journey Flow
- **Test**: `RequestPassengerJourney_CompleteFlow_FromRequestToCompletion`
  - **Scenario**: Request journey from floor 0 to floor 5
  - **Verify**: 
    - Hall call created
    - Request created
    - Elevator assigned
    - Elevator moves to source floor
    - Elevator picks up passenger
    - Elevator moves to destination
    - Hall call completed
    - Request completed

- **Test**: `RequestPassengerJourney_MultiplePassengersSameHallCall_AllDestinationsAdded`
  - **Scenario**: Two passengers request same hall call with different destinations
  - **Verify**: Both destinations added to elevator

- **Test**: `RequestPassengerJourney_ElevatorAlreadyAtFloor_AddsDestinationOnly`
  - **Scenario**: Request when elevator already at source floor
  - **Verify**: Destination added, no duplicate hall call floor

#### 2.2 Hall Call Completion
- **Test**: `ProcessTick_ElevatorArrivesAtHallCallFloor_CompletesHallCall`
  - **Scenario**: Elevator in LOADING state at hall call floor
  - **Verify**: Hall call marked COMPLETED, removed from elevator

- **Test**: `ProcessTick_MultipleHallCallsAtSameFloor_CompletesAll`
  - **Scenario**: Multiple hall calls (UP and DOWN) at same floor
  - **Verify**: Only matching direction hall call completed

- **Test**: `ProcessTick_HallCallCompleted_RemovesFromElevator`
  - **Scenario**: Hall call completed
  - **Verify**: Hall call ID removed from elevator's assigned list

---

### 3. Multi-Elevator Coordination (5 tests)

#### 3.1 Concurrent Operations
- **Test**: `MultipleElevators_IndependentMovement_NoInterference`
  - **Scenario**: Two elevators moving to different floors
  - **Verify**: Each elevator operates independently

- **Test**: `MultipleElevators_AssignToDifferentElevators_NoConflict`
  - **Scenario**: Two hall calls assigned to different elevators
  - **Verify**: Each elevator handles its own hall call

- **Test**: `MultipleElevators_OneIdleOneMoving_LoadBalancing`
  - **Scenario**: One elevator idle, one moving
  - **Verify**: New hall calls prefer idle elevator

#### 3.2 Elevator Selection
- **Test**: `MultipleElevators_AllMoving_SelectsBestAvailable`
  - **Scenario**: All elevators moving, new hall call arrives
  - **Verify**: Best available elevator selected based on strategy

- **Test**: `MultipleElevators_QueueDistribution_EvenlyDistributed`
  - **Scenario**: Multiple pending hall calls
  - **Verify**: Hall calls distributed across available elevators

---

### 4. Rate Limiting Integration (4 tests)

#### 4.1 Rate Limit Enforcement
- **Test**: `RequestHallCall_RateLimitExceeded_RejectsRequest`
  - **Scenario**: Exceed global rate limit
  - **Verify**: Request rejected, metrics updated

- **Test**: `RequestHallCall_PerSourceRateLimitExceeded_RejectsRequest`
  - **Scenario**: Exceed per-source rate limit
  - **Verify**: Request rejected for that source only

- **Test**: `RequestHallCall_RateLimitResets_AllowsNewRequests`
  - **Scenario**: Wait for rate limit window to expire
  - **Verify**: New requests accepted after window

- **Test**: `RequestHallCall_DifferentSources_IndependentLimits`
  - **Scenario**: Multiple sources making requests
  - **Verify**: Each source has independent limit

---

### 5. Metrics Collection Integration (4 tests)

#### 5.1 Metrics During Operations
- **Test**: `RequestHallCall_UpdatesMetrics_TotalAndAcceptedIncremented`
  - **Scenario**: Successful hall call request
  - **Verify**: TotalRequests and AcceptedRequests incremented

- **Test**: `RequestHallCall_Rejected_UpdatesMetrics_TotalAndRejectedIncremented`
  - **Scenario**: Rejected hall call (invalid floor)
  - **Verify**: TotalRequests and RejectedRequests incremented

- **Test**: `ProcessTick_CompletesHallCall_UpdatesMetrics`
  - **Scenario**: Hall call completed during tick
  - **Verify**: CompletedHallCalls incremented

- **Test**: `ProcessTick_UpdatesGauges_PendingAndActiveElevators`
  - **Scenario**: Process tick with pending hall calls and moving elevators
  - **Verify**: PendingHallCalls and ActiveElevators updated

---

### 6. Tick Processing Integration (5 tests)

#### 6.1 Tick Sequence
- **Test**: `ProcessTick_AssignsPendingHallCalls_ThenProcessesElevators`
  - **Scenario**: Pending hall call exists
  - **Verify**: Hall call assigned before elevator processing

- **Test**: `ProcessTick_ProcessesAllElevators_InOrder`
  - **Scenario**: Multiple elevators in different states
  - **Verify**: All elevators processed in ID order

- **Test**: `ProcessTick_CompletesHallCalls_AfterElevatorProcessing`
  - **Scenario**: Elevator arrives at hall call floor
  - **Verify**: Hall call completed after elevator state updated

- **Test**: `ProcessTick_UpdatesMetrics_AtEnd`
  - **Scenario**: Complete tick processing
  - **Verify**: Metrics updated after all operations

#### 6.2 State Transitions
- **Test**: `ProcessTick_MultipleTicks_CompleteElevatorJourney`
  - **Scenario**: Elevator moves from floor 0 to floor 5
  - **Verify**: IDLE → MOVING → LOADING → MOVING → LOADING → IDLE

---

### 7. Error Handling Integration (4 tests)

#### 7.1 Invalid Input Handling
- **Test**: `RequestHallCall_InvalidFloor_RejectsAndUpdatesMetrics`
  - **Scenario**: Request with floor out of range
  - **Verify**: Request rejected, metrics updated, no side effects

- **Test**: `RequestHallCall_InvalidDirection_RejectsAndUpdatesMetrics`
  - **Scenario**: Request with IDLE direction
  - **Verify**: Request rejected, metrics updated

- **Test**: `RequestPassengerJourney_SameSourceAndDestination_Rejects`
  - **Scenario**: Source equals destination
  - **Verify**: Request rejected, no hall call created

#### 7.2 Edge Cases
- **Test**: `RequestHallCall_QueueAtCapacity_RejectsAndUpdatesMetrics`
  - **Scenario**: Queue at maximum capacity
  - **Verify**: Request rejected, QueueFullRejections incremented

---

### 8. Concurrent Request Handling (3 tests)

#### 8.1 Thread Safety
- **Test**: `RequestHallCall_ConcurrentRequests_ThreadSafe`
  - **Scenario**: Multiple threads requesting hall calls simultaneously
  - **Verify**: No race conditions, all requests handled correctly

- **Test**: `ProcessTick_ConcurrentWithRequests_ThreadSafe`
  - **Scenario**: ProcessTick called while requests arrive
  - **Verify**: No data corruption, consistent state

- **Test**: `GetStatus_ConcurrentWithOperations_ReturnsConsistentSnapshot`
  - **Scenario**: GetStatus called during tick processing
  - **Verify**: Returns consistent snapshot, no exceptions

---

## Test Summary

| Category | Test Count | Priority |
|----------|------------|----------|
| Building + Elevator + Scheduling | 8 | P1 |
| Complete Request Lifecycle | 6 | P1 |
| Multi-Elevator Coordination | 5 | P1 |
| Rate Limiting Integration | 4 | P2 |
| Metrics Collection | 4 | P2 |
| Tick Processing | 5 | P1 |
| Error Handling | 4 | P2 |
| Concurrent Operations | 3 | P1 |
| **Total** | **39** | |

---

## Test Infrastructure Requirements

### Test Helpers Needed
1. **BuildingTestHelper** - Factory for creating Building with test dependencies
2. **IntegrationTestBuilder** - Builder for setting up integration scenarios
3. **TickSimulator** - Helper to simulate multiple ticks
4. **MetricsVerifier** - Helper to verify metrics changes

### Test Data
- Standard configuration: 4 elevators, 10 floors
- Test scenarios: Various floor combinations, directions, elevator states

---

## Success Criteria

### Functional
- ✅ All 39 tests passing
- ✅ No regressions in existing unit tests
- ✅ All integration points verified

### Quality
- ✅ Tests are readable and maintainable
- ✅ Tests execute quickly (< 5 seconds total)
- ✅ Tests are isolated (no shared state between tests)

---

## Implementation Order

### Phase 1: Core Integration (Priority 1)
1. Building + Elevator + Scheduling Strategy (8 tests)
2. Complete Request Lifecycle (6 tests)
3. Multi-Elevator Coordination (5 tests)
4. Tick Processing (5 tests)
5. Concurrent Operations (3 tests)

**Total Phase 1: 27 tests**

### Phase 2: Supporting Features (Priority 2)
1. Rate Limiting Integration (4 tests)
2. Metrics Collection (4 tests)
3. Error Handling (4 tests)

**Total Phase 2: 12 tests**

---

## Notes

- Integration tests use real implementations (no mocks for core domain logic)
- Infrastructure components (Logger, Metrics) can be mocked for isolation
- Tests should verify both success and failure paths
- Each test should be independent and runnable in isolation
- Use descriptive test names following pattern: `Component_Scenario_ExpectedResult`

---

**Ready for Implementation** ✅
