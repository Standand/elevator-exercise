# Testable Files - Elevator Control System

**Date:** January 23, 2026  
**Total Files:** 35 source files  
**Testable Files:** 28 files

---

## ✅ Domain Layer (High Priority - 100% Coverage Target)

### Entities (4 files)
1. ✅ **Building.cs** - Aggregate root, coordination logic
   - Status: 8 tests implemented
2. ✅ **Elevator.cs** - State machine, movement logic
   - Status: 12 tests implemented
3. ⏳ **HallCall.cs** - Hall call state transitions
   - Status: 3 tests needed
4. ⏳ **Request.cs** - Request state transitions
   - Status: 2 tests needed

### ValueObjects (9 files)
5. ⏳ **Direction.cs** - Factory method, equality
   - Status: 2 tests needed
6. ⏳ **ElevatorState.cs** - Factory method, equality
   - Status: 2 tests needed
7. ⏳ **HallCallStatus.cs** - Factory method, equality
   - Status: 2 tests needed
8. ⏳ **RequestStatus.cs** - Factory method, equality
   - Status: 2 tests needed
9. ⏳ **Journey.cs** - Validation, direction calculation
   - Status: 3 tests needed
10. ⏳ **DestinationSet.cs** - Next/furthest destination logic
    - Status: 6 tests needed
11. ⏳ **HallCallQueue.cs** - Queue operations
    - Status: 5 tests needed
12. ⏳ **ElevatorStatus.cs** - Snapshot creation
    - Status: 1 test needed
13. ⏳ **BuildingStatus.cs** - Snapshot creation
    - Status: 1 test needed

### Services (3 files)
14. ⏳ **DirectionAwareStrategy.cs** - Scheduling algorithm
    - Status: 4 tests needed
15. ⏳ **NearestFirstStrategy.cs** - Alternative scheduling
    - Status: 3 tests needed
16. ✅ **ISchedulingStrategy.cs** - Interface (no tests needed)

---

## ⏳ Common Layer (High Priority - 90% Coverage Target)

### Utilities (2 files)
17. ⏳ **Result.cs** - Success/failure pattern
    - Status: 3 tests needed
18. ⏳ **RateLimiter.cs** - Rate limiting logic
    - Status: 4 tests needed

---

## ⏳ Infrastructure Layer (Medium Priority - 80% Coverage Target)

### Configuration (2 files)
19. ⏳ **SimulationConfiguration.cs** - Configuration model
    - Status: 1 test needed
20. ⏳ **ConfigurationLoader.cs** - JSON loading, validation
    - Status: 5 tests needed

### Logging (2 files)
21. ✅ **ILogger.cs** - Interface (no tests needed)
22. ⏳ **ConsoleLogger.cs** - Console output (low priority)
    - Status: 2 tests needed (optional)

### Metrics (3 files)
23. ✅ **IMetrics.cs** - Interface (no tests needed)
24. ⏳ **SystemMetrics.cs** - Thread-safe counters
    - Status: 3 tests needed
25. ⏳ **MetricsSnapshot.cs** - Snapshot model
    - Status: 1 test needed

### Time (2 files)
26. ✅ **ITimeService.cs** - Interface (no tests needed)
27. ⏳ **SystemTimeService.cs** - Thin wrapper (low priority)
    - Status: 1 test needed (optional)

---

## ⏳ Application Layer (Medium Priority - 70% Coverage Target)

### Services (3 files)
28. ⏳ **ElevatorSimulationService.cs** - Simulation loop
    - Status: 2 tests needed (integration level)
29. ⏳ **RandomRequestGenerator.cs** - Request generation
    - Status: 2 tests needed (integration level)
30. ⏳ **SystemOrchestrator.cs** - Service coordination
    - Status: 2 tests needed (integration level)

---

## ❌ Excluded from Unit Testing

### Entry Point (1 file)
31. **Program.cs** - Application entry point
    - Reason: Manual DI, no business logic

---

## 📊 Testing Priority Matrix

| Layer | Files | Tests Needed | Priority | Effort |
|-------|-------|--------------|----------|--------|
| **Domain/Entities** | 4 | 25 (5 done) | ⭐⭐⭐⭐⭐ | 8h |
| **Domain/ValueObjects** | 9 | 24 | ⭐⭐⭐⭐⭐ | 6h |
| **Domain/Services** | 2 | 7 | ⭐⭐⭐⭐⭐ | 3h |
| **Common** | 2 | 7 | ⭐⭐⭐⭐ | 3h |
| **Infrastructure/Config** | 2 | 6 | ⭐⭐⭐ | 3h |
| **Infrastructure/Metrics** | 2 | 4 | ⭐⭐⭐ | 2h |
| **Infrastructure/Logging** | 1 | 2 | ⭐ | 1h |
| **Application** | 3 | 6 | ⭐⭐ | 4h |
| **Total** | **25** | **81** | | **30h** |

---

## 🎯 Recommended Testing Order

### Phase 1: Domain Layer (CURRENT - 20 tests remaining)
1. ✅ Building.cs (8 tests) - DONE
2. ✅ Elevator.cs (12 tests) - DONE
3. ⏳ **DestinationSet.cs** (6 tests) - NEXT
4. ⏳ **DirectionAwareStrategy.cs** (4 tests)
5. ⏳ **Journey.cs** (3 tests)
6. ⏳ **HallCall.cs** (3 tests)
7. ⏳ **HallCallQueue.cs** (5 tests)
8. ⏳ **NearestFirstStrategy.cs** (3 tests)
9. ⏳ **Direction.cs** (2 tests)
10. ⏳ **ElevatorState.cs** (2 tests)
11. ⏳ **HallCallStatus.cs** (2 tests)
12. ⏳ **RequestStatus.cs** (2 tests)
13. ⏳ **Request.cs** (2 tests)
14. ⏳ **ElevatorStatus.cs** (1 test)
15. ⏳ **BuildingStatus.cs** (1 test)

**Subtotal:** 56 tests, 17 hours

### Phase 2: Common Layer (7 tests)
16. ⏳ **Result.cs** (3 tests)
17. ⏳ **RateLimiter.cs** (4 tests)

**Subtotal:** 7 tests, 3 hours

### Phase 3: Infrastructure (12 tests)
18. ⏳ **ConfigurationLoader.cs** (5 tests)
19. ⏳ **SystemMetrics.cs** (3 tests)
20. ⏳ **SimulationConfiguration.cs** (1 test)
21. ⏳ **MetricsSnapshot.cs** (1 test)
22. ⏳ **ConsoleLogger.cs** (2 tests - optional)

**Subtotal:** 12 tests, 6 hours

### Phase 4: Application (6 tests - Integration level)
23. ⏳ **ElevatorSimulationService.cs** (2 tests)
24. ⏳ **RandomRequestGenerator.cs** (2 tests)
25. ⏳ **SystemOrchestrator.cs** (2 tests)

**Subtotal:** 6 tests, 4 hours

---

## 📈 Current Progress

**Tests Implemented:** 22 / 81 (27%)  
**Tests Passing:** 22 / 22 (100%)  
**Coverage Estimate:** ~30% (Domain entities only)

**Target:** 81 unit tests + 20 integration tests = **101 total tests**

---

## 🎯 Next Immediate Tasks

1. **DestinationSet.cs** (6 tests, 2 hours) - Critical for floor 0 edge case
2. **DirectionAwareStrategy.cs** (4 tests, 1.5 hours) - Core scheduling logic
3. **Journey.cs** (3 tests, 1 hour) - Input validation
4. **HallCall.cs** (3 tests, 1 hour) - State transitions
5. **HallCallQueue.cs** (5 tests, 1.5 hours) - Queue operations

**Next 5 files:** 21 tests, ~7 hours

---

## 📝 Notes

### High-Value Tests (Must Have)
- ✅ Building request validation
- ✅ Elevator state machine
- ⏳ DestinationSet floor 0 edge case
- ⏳ DirectionAwareStrategy algorithm
- ⏳ Journey validation
- ⏳ Result pattern
- ⏳ RateLimiter sliding window

### Medium-Value Tests (Should Have)
- ⏳ HallCallQueue operations
- ⏳ Value object factories
- ⏳ Configuration validation
- ⏳ SystemMetrics thread safety

### Low-Value Tests (Nice to Have)
- ⏳ ConsoleLogger output
- ⏳ SystemTimeService wrapper
- ⏳ Snapshot models

---

**Ready to implement remaining Domain layer tests! 🚀**
