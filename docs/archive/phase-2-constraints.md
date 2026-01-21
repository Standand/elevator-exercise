# Phase 2 - Constraints

## 1. Building Constraints

### Fixed Parameters (from Problem Statement)
- **Default Floors:** 10
- **Default Elevators:** 4

### Configurable Ranges
**Floors:**
- **Minimum:** 2 floors (minimum functional building)
- **Maximum:** 100 floors (reasonable high-rise limit)
- **Default:** 10 floors (as per problem statement)
- **Validation:** Reject configurations outside range, use default

**Elevators:**
- **Minimum:** 1 elevator (single elevator still functional)
- **Maximum:** 10 elevators (keeps complexity manageable)
- **Default:** 4 elevators (as per problem statement)
- **Validation:** Reject configurations outside range, use default

### Floor Numbering
**Display (User-Facing):**
- **Range:** 1-10 (matches real buildings, includes ground floor)
- **Reason:** Visibility and intuitive understanding

**Internal (Data Structures):**
- **Range:** 0-9 (zero-indexed)
- **Reason:** Simplifies arrays, algorithms, calculations
- **Conversion:** `internalFloor = displayFloor - 1`

**Example:**
```csharp
// User sees: Floor 1, 2, 3, ..., 10
// Code uses: Floor 0, 1, 2, ..., 9
int[] elevatorPositions = new int[10];  // Index 0-9
Console.WriteLine($"Elevator at floor {position + 1}");  // Display 1-10
```

### Elevator Capacity
- **Passenger Count:** Not tracked (infinite capacity assumed)
- **Reasoning:** Weight limits are out of scope per problem statement
- **Simplification:** Elevator can serve unlimited destinations per trip

---

## 2. Timing Constraints

### Movement Timing
**Default:** 10 seconds to move between adjacent floors

**Consistency:**
- UP movement time = DOWN movement time (same duration)
- Empty elevator = Loaded elevator (weight/speed not modeled)
- All floor transitions take same time

**Granularity:**
- **Track in:** Seconds (keep it simple)
- **Not:** Milliseconds (unnecessary precision)

**Accuracy:**
- **Target:** Exact timing (10.000 seconds)
- **Acceptable variance:** ±10-100ms due to OS thread scheduling
- **Reasoning:** Deterministic simulation preferred for testing

### Loading/Unloading Timing
**Default:** 10 seconds for passengers to board/exit

**Consistency:**
- Loading time = Unloading time (same duration)
- Same loading time for all floors
- Same loading time regardless of passenger count

### Test Mode Timing
**Purpose:** Fast testing without waiting real 10 seconds

**Configuration:** Per-operation configuration (not global multiplier)

**Example:**
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

**Speedup:** 100x faster (10 seconds → 100ms)

### Time Granularity
- **Unit:** Milliseconds in configuration
- **Internal tracking:** Seconds for simplicity
- **Conversion:** `int seconds = configMs / 1000`

---

## 3. Request Generation Constraints

### Request Frequency
**Default:** 1 request every 5 seconds (12 requests/minute)

**Configurable Range:**
- **Minimum frequency:** 1 request per 60 seconds (slow, 1/min)
- **Maximum frequency:** 1 request per 1 second (fast, 60/min)
- **Reasoning:** Prevents overwhelming system or duplicate request spam

**Generation Mode:**
- **Duration:** Infinite (until simulation stopped)
- **Control:** Start/stop via control commands

### Request Distribution
**Floor Distribution:**
- **Pattern:** Uniform random across all floors (1-10)
- **Not modeled:** Busy floors (e.g., lobby), peak hours
- **Reasoning:** Simplicity for Phase 1

**Direction Distribution:**
- **Derived:** Direction calculated from source/destination
- **Not configured:** No manual direction distribution percentages

### Request Idempotency
**Deduplication Strategy:**
- **Key:** (Floor, Direction) tuple
- **Behavior:** Multiple requests for same (floor, direction) merge into one hall call
- **Destinations:** Accumulated in a set (deduplicated)

**Example:**
```
Request 1: Floor 5 → Floor 8 (UP)   → HallCall(5, UP) destinations=[8]
Request 2: Floor 5 → Floor 10 (UP)  → HallCall(5, UP) destinations=[8, 10]
Request 3: Floor 5 → Floor 8 (UP)   → HallCall(5, UP) destinations=[8, 10] (no change)
```

**Maximum Unique Hall Calls:**
- Floor 1: 1 (UP only)
- Floor 10: 1 (DOWN only)
- Floors 2-9: 16 (8 floors × 2 directions)
- **Total:** 18 unique hall calls maximum

### Concurrent Request Limits
**Maximum Pending Hall Calls:** 18 (theoretical maximum)

**Practical Queue Size:** 15-20 hall calls

**Behavior at Limit:**
- New request for existing (floor, direction) → Merge destinations
- System naturally bounded by physical constraints
- No artificial queue rejection needed

### Request Validation
**Source Floor:**
- Must be in range [1, floors]
- Must be valid integer

**Destination Floor:**
- Must be in range [1, floors]
- Must be ≠ source floor
- Must be valid integer

**Invalid Request Handling:**
- Log error with details
- Reject request
- Continue operation (don't crash)

---

## 4. Data Size Constraints

### Request Identifiers
**Type:** `int` (32-bit signed integer)

**Range:** -2,147,483,648 to 2,147,483,647

**Expected Usage:**
- Hundreds to thousands of requests per session
- `int` is more than sufficient

**ID Generation:**
- Sequential: 1, 2, 3, ...
- Thread-safe increment using `Interlocked.Increment()`

### Log Retention
**Console Output:** Unlimited (scrolling console)

**Optional File Logging:**
- **Retention:** Last 2,500 lines
- **Behavior:** Ring buffer (oldest discarded when full)
- **Reasoning:** Prevent unbounded memory growth in long-running simulations

**Log Rotation:**
- Not required for Phase 1
- Can be added later if file logging implemented

### Metrics Storage
**Counters (no history):**
```csharp
int totalRequestsReceived;
int totalRequestsCompleted;
int totalRequestsPending;
```

**Running Averages (no history arrays):**
```csharp
double averageWaitTime;      // Updated incrementally
double averageTravelTime;    // Updated incrementally
```

**Calculation:**
```csharp
// Incremental average (no array needed)
averageWaitTime = ((averageWaitTime * count) + newWaitTime) / (count + 1);
```

**No Historical Data:**
- No arrays of past requests
- No time-series data
- Keeps memory footprint minimal

---

## 5. Performance Constraints

### Memory Constraints
**Target:** No hard limit (simple simulation)

**Expected Usage:**
- Elevator state: 4 elevators × ~1 KB = 4 KB
- Hall calls: 18 max × ~1 KB = 18 KB
- Logs: 2,500 lines × ~200 bytes = 500 KB
- **Total:** < 10 MB typical usage

**Acceptable:** Up to 100 MB for long-running simulations

**Not Required:** Memory optimization, garbage collection tuning

### CPU Constraints
**Target:** Single core sufficient

**Usage:**
- 4 elevator tasks (lightweight, mostly sleeping)
- 1 request generator task
- Simple scheduling algorithm (O(n) where n=4)

**Not Required:**
- Multi-core optimization
- CPU-intensive algorithms
- Performance profiling

### Response Time Constraints
**Request Assignment:**
- **Target:** < 1 second (P99)
- **Definition:** 99% of requests assigned within 1 second
- **Acceptable:** Occasional 1-2 second delay under load

**Status Query:**
- **Target:** < 100 milliseconds
- **Reasoning:** Read operation, no complex computation

**Simulation Tick:**
- **Accuracy:** ±100ms variance from configured time
- **Reasoning:** OS thread scheduling not perfectly precise

---

## 6. Deployment Constraints

### Platform
**Target:** Cross-platform

**Supported:**
- Windows (10, 11)
- Linux (Ubuntu 20.04+, other distributions)
- macOS (10.15+)

**Reasoning:** .NET 8 is cross-platform

### Runtime Environment
**Framework:** .NET 8

**Runtime Required:** .NET 8 Runtime or SDK

**Dependencies:**
- Standard library only (System.*)
- Microsoft.Extensions.Logging (optional, for structured logging)
- No external NuGet packages required for core functionality

**Deployment Mode:**
- **Development:** Requires .NET 8 SDK
- **Production:** Self-contained or framework-dependent (configurable)

### Configuration
**Location:** Same directory as executable

**Filename:** `appsettings.json`

**Behavior if Missing:**
- Use hardcoded defaults
- Log warning: "Config file not found, using defaults"
- Continue operation

**Example Structure:**
```
elevator-system/
  ├── ElevatorSystem.exe (or .dll)
  ├── appsettings.json
  └── logs/ (optional)
```

---

## 7. Concurrency Constraints

### Threading Model
**Strategy:** Task-based async/await (C# modern pattern)

**Implementation:**
```csharp
// Each elevator runs as async task
public async Task RunAsync(CancellationToken ct) 
{
    while (!ct.IsCancellationRequested) 
    {
        await ProcessNextActionAsync();
        await Task.Delay(movementTimeMs, ct);
    }
}

// Start all elevators
var elevatorTasks = elevators.Select(e => e.RunAsync(cts.Token));
await Task.WhenAll(elevatorTasks);
```

**Task Count:**
- 4 elevator tasks (one per elevator)
- 1 request generator task
- 1 main coordination task
- **Total:** ~6 concurrent tasks (thread pool managed)

**Advantages:**
- ✅ Modern C# pattern
- ✅ Efficient resource usage (thread pool)
- ✅ Easy cancellation (graceful shutdown)
- ✅ Async I/O support (logging, file operations)

### Synchronization Strategy
**Primary Approach:** Global lock for critical sections + concurrent collections

**Critical Sections (Global Lock):**
```csharp
private static readonly object _globalLock = new object();

lock (_globalLock) 
{
    // Request assignment logic
    // Elevator state transitions that affect multiple elevators
}
```

**Lock-Free Collections:**
```csharp
private ConcurrentDictionary<(int floor, Direction dir), HallCallRequest> _hallCalls;
private ConcurrentQueue<string> _logMessages;
```

**Locking Strategy:**
- Use global lock for complex multi-elevator operations
- Use concurrent collections for simple add/remove operations
- Keep critical sections small (< 10 lines)

**Advantages:**
- ✅ Simple, correct by construction
- ✅ No deadlock risk (single lock)
- ✅ Good enough performance for 4 elevators
- ✅ Easy to reason about

### Deadlock Prevention
**Risk:** Very low (single global lock)

**Strategy:** Not needed for Phase 1

**If multiple locks added later:**
- Lock ordering: Always acquire in same order
- Timeouts: Use `Monitor.TryEnter()` with timeout
- Avoid nested locks where possible

---

## 8. Configuration Constraints

### Validation Rules
**Floors:**
```csharp
if (config.Floors < 2 || config.Floors > 100) 
{
    logger.LogWarning($"Invalid floors {config.Floors}, using default 10");
    config.Floors = 10;
}
```

**Elevators:**
```csharp
if (config.Elevators < 1 || config.Elevators > 10) 
{
    logger.LogWarning($"Invalid elevators {config.Elevators}, using default 4");
    config.Elevators = 4;
}
```

**Timing:**
```csharp
if (config.MovementTimeMs < 100 || config.MovementTimeMs > 60000) 
{
    logger.LogWarning($"Invalid movement time {config.MovementTimeMs}ms, using default 10000ms");
    config.MovementTimeMs = 10000;
}
```

**Request Frequency:**
```csharp
if (config.RequestFrequencyMs < 1000 || config.RequestFrequencyMs > 60000) 
{
    logger.LogWarning($"Invalid request frequency {config.RequestFrequencyMs}ms, using default 5000ms");
    config.RequestFrequencyMs = 5000;
}
```

### Configuration Limits
| Parameter | Minimum | Maximum | Default |
|-----------|---------|---------|---------|
| Floors | 2 | 100 | 10 |
| Elevators | 1 | 10 | 4 |
| Movement Time | 100ms | 60000ms (1 min) | 10000ms (10s) |
| Loading Time | 100ms | 60000ms (1 min) | 10000ms (10s) |
| Request Frequency | 1000ms (1s) | 60000ms (60s) | 5000ms (5s) |

### Configuration Reload
**Strategy:** Restart required

**Reasoning:** Simplicity for Phase 1

**Behavior:**
- Configuration loaded at startup
- Changes require application restart
- No hot reload support

**Future:** Could add configuration watcher for hot reload

---

## 9. Error Handling Constraints

### Invalid Request Handling
**Scenarios:**
- Floor out of range (floor 11 in 10-floor building)
- Destination = Source
- Invalid data types (floor = "abc")

**Behavior:**
```csharp
if (sourceFloor < 1 || sourceFloor > maxFloors) 
{
    logger.LogError($"Invalid source floor {sourceFloor}, ignoring request");
    return;  // Continue operation, don't crash
}
```

**Error Response:**
- Log error with details
- Reject request (don't process)
- Continue operation (graceful degradation)
- Increment error counter

### System Error Handling
**Scenarios:**
- Unexpected exceptions in elevator logic
- Task cancellation
- File I/O errors (optional logging)

**Strategy:**
```csharp
try 
{
    await ProcessElevatorActionAsync();
}
catch (OperationCanceledException) 
{
    // Expected during shutdown
    logger.LogInformation("Elevator task cancelled, shutting down");
}
catch (Exception ex) 
{
    logger.LogError(ex, "Unexpected error in elevator processing");
    // Attempt recovery: reset elevator to IDLE state
    elevator.Reset();
}
```

**Recovery:**
- Log exception with stack trace
- Attempt graceful recovery (reset to safe state)
- Don't crash entire system for single elevator failure

### Error Rate Tolerance (Think if required. it is not necessarily required. remove if it ambiguous)
**Invalid Requests:**
- **Acceptable:** < 5% of total requests
- **Reasoning:** Generator might occasionally produce invalid data

**System Errors:**
- **Acceptable:** < 0.1% of operations
- **Reasoning:** Code bugs should be rare

**Fatal Errors:**
- **Target:** 0% (system should never crash)
- **Behavior:** Log error, attempt recovery, continue if possible

**Monitoring:**
```csharp
double errorRate = (double)errorCount / totalRequests;
if (errorRate > 0.05)  // 5%
{
    logger.LogWarning($"High error rate detected: {errorRate:P}");
}
```

---

## 10. Testing Constraints

### Unit Test Execution Time
**Target:** < 100ms per unit test

**Reasoning:** Pure logic tests, no I/O or delays

**Total Suite:** < 1 second for all unit tests (100+ tests)

**Coverage:**
- Scheduler logic
- State machine transitions
- Validation logic
- Configuration parsing

### Integration Test Execution Time
**Target:** < 3 seconds per integration test (worst case)

**Typical:** < 1 second with accelerated timing (100ms)

**Example:**
```csharp
// Test elevator moving 3 floors with 100ms timing
// Expected time: 3 floors × 100ms = 300ms
// Plus test overhead: ~500ms total
```

**Total Suite:** < 30 seconds for all integration tests

### Test Coverage Target
**Goal:** 90% code coverage

**Priority Areas (must cover):**
- Request processing and validation
- Elevator state machine
- Scheduling algorithms
- Hall call idempotency

**Lower Priority (can skip):**
- Logging statements
- Configuration parsing edge cases
- UI/console output formatting

**Measurement:** Use built-in .NET coverage tools

### Test Timing Configuration
**Use accelerated timing:**
```json
{
  "testMode": {
    "movementTimeMs": 100,
    "loadingTimeMs": 100
  }
}
```

**100x speedup:**
- Production: 10 seconds → Test: 100ms
- 30-second scenario → 300ms test execution
- Entire test suite runs quickly

---

## Summary Table

| Constraint Category | Key Limits |
|---------------------|------------|
| **Floors** | 2-100 (default: 10) |
| **Elevators** | 1-10 (default: 4) |
| **Movement Time** | 100ms-60s (default: 10s) |
| **Loading Time** | 100ms-60s (default: 10s) |
| **Request Frequency** | 1s-60s (default: 5s) |
| **Max Hall Calls** | 18 (theoretical), 15-20 (practical) |
| **Request ID Type** | int (32-bit) |
| **Log Retention** | 2,500 lines (optional file logging) |
| **Memory Usage** | < 100 MB |
| **Assignment Latency** | < 1s (P99) |
| **Query Latency** | < 100ms |
| **Threading** | Task-based async/await |
| **Platform** | Cross-platform (.NET 8) |
| **Error Rate** | < 1% acceptable |
| **Test Coverage** | 90% target |
| **Test Execution** | Unit: <100ms, Integration: <3s |
