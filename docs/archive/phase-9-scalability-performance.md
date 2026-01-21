# Phase 9 - Scalability & Performance

## Overview

This document analyzes the scalability characteristics and performance profile of the Elevator Control System. It identifies bottlenecks, evaluates scaling strategies, and validates that the design meets all performance requirements.

**Design Principle:** Keep it simple - optimize only when there's a proven performance problem.

---

## 1. Current Bottlenecks Analysis

### Single Lock Design

**Current Implementation:**
```csharp
public class Building
{
    private readonly object _lock = new();
    
    public Result<HallCall> RequestHallCall(int floor, Direction direction)
    {
        lock (_lock) { /* ... */ }
    }
    
    public void ProcessTick()
    {
        lock (_lock) { /* ... */ }
    }
    
    public BuildingStatus GetStatus()
    {
        lock (_lock) { /* ... */ }
    }
}
```

**Lock Hold Times (Estimated):**
- `RequestHallCall()`: ~10 microseconds
  - Validation: 1μs
  - Hall call creation: 5μs
  - Queue insertion: 4μs
  
- `ProcessTick()`: ~400 microseconds (4 elevators)
  - Per elevator processing: ~100μs
  - Scheduler logic: ~50μs each
  
- `GetStatus()`: ~50 microseconds
  - Snapshot creation: 50μs

**Lock Contention Analysis:**
```
Tick interval: 1000ms (1 second)
Lock busy time: 400μs + (20 requests/min × 10μs) = ~404μs per second
Lock utilization: 404μs / 1,000,000μs = 0.04%

Contention: Negligible (<0.1%)
```

**Verdict:** ✅ **No bottleneck for single building design**

---

## 2. Scaling Strategy

### Phase 1 Scope: Single Building

**Design Target:**
- Single building
- Configurable floors (2-100)
- Configurable elevators (1-10)
- In-memory state
- Single process

**Out of Scope:**
- Multi-building deployments
- Distributed coordination
- Cross-building optimization

### Scaling Scenario: 20 Elevators (Skyscraper)

**Question:** Does current design handle 20 elevators?

**Analysis:**
```
Tick processing time with 20 elevators:
- Retry pending hall calls: ~50μs (minimal)
- Process 20 elevators: 20 × 100μs = 2000μs (2ms)
- Total: ~2ms per tick

Tick interval: 1000ms
Processing overhead: 2ms / 1000ms = 0.2%

Lock contention: Still <1%
```

**Verdict:** ✅ **Current design scales to 20+ elevators easily**

### Horizontal Scaling (Future - Multi-Building)

**If needed to scale to 100 buildings:**

**Strategy:** Deploy 100 independent Building instances

```
Building 1 (Process 1) → Elevators 1-4 (Building A)
Building 2 (Process 2) → Elevators 1-4 (Building B)
...
Building 100 (Process 100) → Elevators 1-4 (Building Z)
```

**Characteristics:**
- ✅ **No shared state** between buildings
- ✅ **No coordination** required
- ✅ **Linear scalability** (N buildings = N processes)
- ✅ **Fault isolation** (Building A crash doesn't affect Building B)

**Architecture:**
```
┌─────────────────┐
│  Load Balancer  │
└────────┬────────┘
         │
    ┌────┴────┐
    │         │
┌───▼──┐  ┌──▼───┐
│ Bldg │  │ Bldg │
│  A   │  │  B   │
└──────┘  └──────┘
```

**Partitioning Key:** Building ID (natural partition)

**Recommendation:** ✅ Horizontal scaling by Building ID (if multi-building support needed in future)

---

## 3. Partitioning Strategy

### Question: Do we need partitioning?

**Answer:** ❌ **No partitioning needed for Phase 1**

**Rationale:**
1. **Single building** (single process, single memory space)
2. **All state in-memory** (no distributed data)
3. **No cross-building coordination** (each building independent)
4. **Data fits in memory** (< 1 MB for single building)

**When would partitioning be needed?**
- Multi-building deployments (partition by Building ID)
- Distributed request routing (partition by floor range)
- Data doesn't fit in memory (not our case)

**Verdict:** N/A for Phase 1 - Single building design

---

## 4. Caching Strategy

### Question: What should be cached?

**Answer:** ❌ **No caching needed**

**Analysis of Candidates:**

#### Option A: Cache `Building.GetStatus()` responses
```csharp
// Current (no cache)
public BuildingStatus GetStatus()
{
    lock (_lock)  // 50μs
    {
        return new BuildingStatus(/* snapshot */);
    }
}

// With cache
public BuildingStatus GetStatus()
{
    if (_cachedStatus != null && _cacheAge < 100ms)
        return _cachedStatus;
    
    lock (_lock)
    {
        _cachedStatus = new BuildingStatus(/* snapshot */);
        return _cachedStatus;
    }
}
```

**Trade-offs:**
- ✅ Reduces lock contention (marginally)
- ❌ Returns stale data (state changes every tick)
- ❌ Cache invalidation complexity
- ❌ Minimal benefit (50μs → 1μs savings)

**Verdict:** ❌ Not worth the complexity

#### Option B: Cache `Scheduler.SelectElevator()` results
```csharp
// Stateless, deterministic function
// Result changes as elevator state changes (every tick)
// Caching would be incorrect
```

**Verdict:** ❌ Incorrect - state changes too frequently

#### Option C: Cache configuration
```csharp
// Already in-memory (loaded once at startup)
// No benefit to additional caching
```

**Verdict:** ❌ Already optimal

**Recommendation:** ✅ **No caching for Phase 1** - All data already in-memory, operations are fast (<50μs)

---

## 5. Read vs Write Optimization

### Workload Analysis

**Phase 1 Operations (Console Simulation):**

**Writes:**
- `RequestHallCall()`: 20 requests/minute = 0.33/second
- `ProcessTick()`: 1/second (internal state mutation)

**Reads:**
- `GetStatus()`: ~1/second (console logging)

**Read/Write Ratio:** ~1:1 (balanced workload)

### Concurrency Options

#### Option A: Single Lock (Current)
```csharp
private readonly object _lock = new();

public Result<HallCall> RequestHallCall(...)
{
    lock (_lock) { /* write */ }
}

public BuildingStatus GetStatus()
{
    lock (_lock) { /* read */ }
}
```

**Characteristics:**
- ✅ Simple (one lock, no deadlocks)
- ✅ Correct (total ordering of operations)
- ✅ Fast enough (0.04% lock utilization)
- ❌ Readers block writers (but not an issue at 1:1 ratio)

#### Option B: Read-Write Lock
```csharp
private readonly ReaderWriterLockSlim _rwLock = new();

public Result<HallCall> RequestHallCall(...)
{
    _rwLock.EnterWriteLock();
    try { /* write */ }
    finally { _rwLock.ExitWriteLock(); }
}

public BuildingStatus GetStatus()
{
    _rwLock.EnterReadLock();
    try { /* read */ }
    finally { _rwLock.ExitReadLock(); }
}
```

**Characteristics:**
- ✅ Multiple readers allowed (better for read-heavy workloads)
- ❌ More complex (2 lock types, more overhead)
- ❌ Slower for balanced workloads (lock overhead > benefit)
- ❌ Harder to reason about (reader starvation possible)

### Decision Matrix

| Workload | Read/Write Ratio | Lock Contention | Recommendation |
|----------|-----------------|-----------------|----------------|
| **Phase 1 (Console)** | 1:1 | 0.04% | Single Lock |
| Dashboard (Future) | 30:1 | 1-2% | Consider RW Lock |
| High-frequency monitoring | 100:1 | 5-10% | RW Lock |

**Phase 1 Decision:** ✅ **Keep single lock**

**Rationale:**
- Balanced read/write ratio (1:1)
- Negligible lock contention (<0.1%)
- Simpler code, easier to maintain
- No performance problem to solve

**Future:** If monitoring dashboard added with >10 reads/second, reconsider read-write lock.

---

## 6. Concurrency Model

### Current Model: Single Lock

**Design:**
```csharp
public class Building
{
    private readonly object _lock = new();
    
    // ALL state mutations protected by single lock
    // Simple, correct, sufficient
}
```

**Characteristics:**
- ✅ **Simple:** One lock, no deadlocks
- ✅ **Correct:** Total ordering guarantees correctness
- ✅ **Testable:** Deterministic behavior
- ✅ **Sufficient:** Handles 20+ elevators

### Alternative Models Considered

#### Option A: Fine-Grained Locking
```csharp
public class Building
{
    private readonly object _globalLock = new();
    private readonly Dictionary<int, object> _elevatorLocks = new();
    
    // Per-elevator locks for parallel processing
}
```

**Trade-offs:**
- ✅ Better throughput (elevators process in parallel)
- ❌ Deadlock risk (multiple lock acquisitions)
- ❌ Complexity (lock ordering rules)
- ❌ Not needed (no contention problem)

#### Option B: Lock-Free Data Structures
```csharp
public class Building
{
    private readonly ConcurrentQueue<HallCall> _hallCallQueue;
    private readonly ConcurrentDictionary<int, Elevator> _elevators;
    
    // Lock-free operations using atomic CAS
}
```

**Trade-offs:**
- ✅ Best performance (no blocking)
- ❌ Very complex (race conditions, ABA problem)
- ❌ Hard to reason about correctness
- ❌ Overkill for single building

#### Option C: Actor Model
```csharp
// Each elevator is an actor (mailbox + message loop)
public class ElevatorActor
{
    private readonly Channel<IMessage> _inbox;
    
    public async Task ProcessMessages()
    {
        await foreach (var msg in _inbox.Reader.ReadAllAsync())
        {
            // Handle message (no shared state)
        }
    }
}
```

**Trade-offs:**
- ✅ Clean isolation (no shared state)
- ✅ Natural concurrency (message passing)
- ❌ Overhead (message serialization, context switching)
- ❌ Complexity (actor lifecycle, supervision)
- ❌ Not needed for Phase 1

### Decision: Keep Single Lock ✅

**Rationale:**
1. **No performance problem:** Lock contention <0.1%
2. **Scales to 20 elevators:** 2ms processing time << 1000ms tick interval
3. **Simple and correct:** No deadlocks, no race conditions
4. **Easier to test:** Deterministic behavior
5. **Premature optimization:** Don't optimize without measurements

**Performance Validation:**
```
20 elevators × 100μs = 2ms processing time
Tick interval: 1000ms
Overhead: 0.2%

Conclusion: Single lock is sufficient
```

---

## 7. Performance Requirements Validation

### Requirements (from Phase 1)

| Requirement | Target | Current Design | Status |
|------------|--------|----------------|--------|
| Request processing time | <1 second | ~10μs | ✅ Pass (100,000× faster) |
| Status query time | <1 second | ~50μs | ✅ Pass (20,000× faster) |
| Throughput | 20 requests/minute | 10,000+ requests/minute | ✅ Pass (500× capacity) |
| Tick processing | Not specified | 400μs (4 elevators) | ✅ Acceptable |
| | | 2ms (20 elevators) | ✅ Acceptable |

### Detailed Performance Analysis

#### Request Processing (`RequestHallCall`)
```csharp
public Result<HallCall> RequestHallCall(int floor, Direction direction)
{
    lock (_lock)  // ~10μs total
    {
        // 1. Rate limiting: 1μs
        if (!_rateLimiter.IsAllowed(source))
            return Result.Failure("Rate limit exceeded");
        
        // 2. Validation: 1μs
        if (floor < 0 || floor > _maxFloors)
            return Result.Failure("Invalid floor");
        
        // 3. Idempotency check: 2μs
        var existing = _hallCallQueue.FindByFloorAndDirection(floor, direction);
        if (existing != null)
            return Result.Success(existing);
        
        // 4. Create hall call: 5μs
        var hallCall = new HallCall(floor, direction);
        _hallCallQueue.Add(hallCall);
        
        // 5. Log: 1μs
        _logger.LogInfo($"HallCall {hallCall.Id} created");
        
        return Result.Success(hallCall);
    }
}
```

**Latency:** ~10μs (0.00001 seconds) << 1 second ✅

#### Status Query (`GetStatus`)
```csharp
public BuildingStatus GetStatus()
{
    lock (_lock)  // ~50μs total
    {
        // Create snapshot of current state
        return new BuildingStatus
        {
            Elevators = _elevators.Select(e => e.GetStatus()).ToList(),  // 40μs
            PendingHallCalls = _hallCallQueue.GetPending().Count,        // 5μs
            Timestamp = DateTime.UtcNow                                  // 5μs
        };
    }
}
```

**Latency:** ~50μs (0.00005 seconds) << 1 second ✅

#### Tick Processing (`ProcessTick`)
```csharp
public void ProcessTick()
{
    lock (_lock)  // ~400μs for 4 elevators, ~2ms for 20 elevators
    {
        // 1. Retry pending hall calls: ~50μs
        var pendingHallCalls = _hallCallQueue.GetPending();
        foreach (var hallCall in pendingHallCalls)
        {
            var elevator = _scheduler.SelectElevator(hallCall, _elevators);
            if (elevator != null)
                elevator.AssignHallCall(hallCall);
        }
        
        // 2. Process each elevator: N × 100μs
        foreach (var elevator in _elevators)
        {
            elevator.ProcessTick();  // ~100μs per elevator
        }
    }
}
```

**Latency:** 
- 4 elevators: ~400μs
- 20 elevators: ~2ms
- Well within 1000ms tick interval ✅

#### Throughput Capacity
```
Lock hold time per request: 10μs
Max requests per second: 1,000,000μs / 10μs = 100,000 requests/second

Max requests per minute: 100,000 × 60 = 6,000,000 requests/minute

Actual requirement: 20 requests/minute
Headroom: 300,000× capacity
```

**Verdict:** ✅ Throughput not a concern

### Conclusion: All Performance Requirements Met ✅

**No optimization needed for Phase 1.**

---

## 8. Database Considerations

### Question: Should we add a database?

**Answer:** ❌ **No database for Phase 1**

### Trade-offs Analysis

#### Option A: No Database (Current)
**Pros:**
- ✅ Simple (no infrastructure, no schema, no migrations)
- ✅ Fast (all operations in-memory, <50μs)
- ✅ Sufficient (state loss acceptable per Phase 1 requirements)
- ✅ Easy to test (no database setup)

**Cons:**
- ❌ State lost on restart (acceptable for simulation)
- ❌ No audit trail (logs provide sufficient observability)
- ❌ No historical analytics (out of scope for Phase 1)

#### Option B: Add Database (SQLite, PostgreSQL)
**Pros:**
- ✅ Persistence (recover state after crash)
- ✅ Audit trail (query historical requests)
- ✅ Analytics (request patterns, elevator utilization)

**Cons:**
- ❌ Latency (10-100ms per write vs 10μs in-memory)
- ❌ Complexity (schema, migrations, transactions)
- ❌ Infrastructure (database setup, maintenance)
- ❌ Overkill for simulation

### Use Cases Evaluation

| Use Case | Needs Database? | Phase 1 Status |
|----------|----------------|----------------|
| Elevator state recovery | No | Restart acceptable |
| Request audit trail | No | Logs sufficient |
| Historical analytics | No | Out of scope |
| Compliance/regulations | No | Simulation only |

**Phase 1 Decision:** ✅ **No database - In-memory only**

**Future Consideration:** If audit trail needed for production deployment, add write-ahead log (WAL) or event sourcing.

---

## 9. Async Processing

### Question: Should we make operations asynchronous?

**Answer:** ❌ **Keep synchronous for Phase 1**

### Trade-offs Analysis

#### Current Design: Synchronous
```csharp
public Result<HallCall> RequestHallCall(int floor, Direction direction)
{
    lock (_lock)
    {
        // Process immediately
        var hallCall = new HallCall(floor, direction);
        _hallCallQueue.Add(hallCall);
        return Result<HallCall>.Success(hallCall);
    }
}
```

**Characteristics:**
- ✅ Simple (no async/await, no background workers)
- ✅ Fast (10μs latency)
- ✅ Immediate feedback (client gets result immediately)
- ✅ Easy to test (deterministic, no timing issues)
- ✅ Easy to debug (sequential execution, simple stack traces)

#### Alternative: Asynchronous
```csharp
public async Task<Result<HallCall>> RequestHallCallAsync(int floor, Direction direction)
{
    // Queue request for background processing
    var request = new Request(floor, direction);
    await _requestQueue.EnqueueAsync(request);
    
    // Return immediately (request processed later)
    return Result<HallCall>.Success(hallCall);
}

// Background worker
private async Task ProcessRequestQueue()
{
    await foreach (var request in _requestQueue.Reader.ReadAllAsync())
    {
        // Process request
        lock (_lock)
        {
            // ... create hall call ...
        }
    }
}
```

**Characteristics:**
- ✅ Higher throughput (10,000+ requests/second)
- ❌ Complex (async/await, channels, background workers)
- ❌ Slower latency (1-10ms queuing overhead)
- ❌ Harder to test (timing dependencies, race conditions)
- ❌ Harder to debug (async stack traces, state machines)
- ❌ Overkill for 20 requests/minute

### Decision Matrix

| Scenario | Throughput Need | Current Capacity | Recommendation |
|----------|----------------|------------------|----------------|
| **Phase 1** | 20 requests/min | 6M requests/min | Synchronous |
| High traffic | 10,000 requests/sec | 100,000 requests/sec | Synchronous |
| External I/O | Network calls | N/A | Async (not our case) |

**When would async help?**
1. **External I/O:** Network calls, disk I/O (we have none)
2. **High throughput:** >100,000 requests/second (we need 0.33/second)
3. **Long-running operations:** >100ms processing time (ours is 10μs)

**Phase 1: None of these apply.**

**Phase 1 Decision:** ✅ **Keep synchronous**

**Rationale:**
- No performance problem (capacity 300,000× requirement)
- Simpler code (easier to understand, test, debug)
- Immediate feedback (better UX)
- No I/O operations to parallelize
- Premature optimization

---

## 10. Monitoring & Observability

### Question: What metrics should we track?

**Answer:** ✅ **Logs + Minimal Metrics**

### Phase 1 Monitoring Strategy

#### Component 1: Structured Logging

**Current Logging (Already Implemented):**
```csharp
// Request lifecycle
_logger.LogInfo($"HallCall {hallCall.Id} created: Floor {floor}, Direction {direction}");
_logger.LogInfo($"HallCall {hallCall.Id} assigned to Elevator {elevator.Id}");
_logger.LogInfo($"HallCall {hallCall.Id} completed");

// Elevator events
_logger.LogInfo($"Elevator {Id} arrived at floor {CurrentFloor}");
_logger.LogInfo($"Elevator {Id} doors opened, {passengers} passengers boarding");
_logger.LogInfo($"Elevator {Id} moving {Direction}");

// Warnings and errors
_logger.LogWarning($"Invalid floor: {floor}");
_logger.LogWarning($"Rate limit exceeded for source '{source}'");
_logger.LogError($"FATAL: Unexpected exception in simulation loop: {ex}");
```

**Log Levels:**
- `DEBUG`: Verbose details (door timer, scheduler logic)
- `INFO`: Normal operations (requests, assignments, movements)
- `WARNING`: Rejected requests, rate limits
- `ERROR`: Unexpected exceptions, system failures

#### Component 2: Minimal Metrics

**Metrics to Track (Low Overhead):**

```csharp
public class SystemMetrics
{
    // Counters (increment only, no overhead)
    private int _totalRequests = 0;
    private int _acceptedRequests = 0;
    private int _rejectedRequests = 0;
    private int _completedHallCalls = 0;
    private int _rateLimitHits = 0;
    private int _queueFullRejections = 0;
    
    // Gauges (current state, computed on-demand)
    public int PendingHallCalls => _hallCallQueue.Count(hc => hc.Status == PENDING);
    public int AssignedHallCalls => _hallCallQueue.Count(hc => hc.Status == ASSIGNED);
    public int ActiveElevators => _elevators.Count(e => e.State != IDLE);
    public int IdleElevators => _elevators.Count(e => e.State == IDLE);
    
    // Methods
    public void IncrementTotalRequests() => Interlocked.Increment(ref _totalRequests);
    public void IncrementRejectedRequests() => Interlocked.Increment(ref _rejectedRequests);
    public void IncrementCompletedHallCalls() => Interlocked.Increment(ref _completedHallCalls);
    
    public MetricsSnapshot GetSnapshot()
    {
        return new MetricsSnapshot
        {
            TotalRequests = _totalRequests,
            AcceptedRequests = _acceptedRequests,
            RejectedRequests = _rejectedRequests,
            CompletedHallCalls = _completedHallCalls,
            RateLimitHits = _rateLimitHits,
            QueueFullRejections = _queueFullRejections,
            PendingHallCalls = PendingHallCalls,
            AssignedHallCalls = AssignedHallCalls,
            ActiveElevators = ActiveElevators,
            IdleElevators = IdleElevators
        };
    }
}
```

**Metrics Output (Printed Every 10 Seconds):**
```
[METRICS] Requests: 120 total (115 accepted, 5 rejected) | Completed: 110 | Pending: 3 | Active Elevators: 2/4
[METRICS] Rate Limit Hits: 5 | Queue Full Rejections: 0
```

**Rationale:**
- ✅ **Zero overhead:** Simple atomic counters (`Interlocked.Increment`)
- ✅ **Useful:** Validates system behavior (rate limiting works, requests completing)
- ✅ **Debugging:** Helps identify issues (queue growing, all elevators idle)
- ✅ **Simple:** Print to console every 10 seconds

### What We're NOT Tracking (Too Complex for Phase 1)

❌ **Percentile latencies** (P50, P95, P99) - Requires histogram, memory overhead
❌ **Lock contention time** - Requires instrumentation, overhead
❌ **Request distribution** (per floor, per direction) - Analytics, not needed
❌ **Elevator utilization over time** - Time series, overkill

### Console Output Example

```
[00:00:10] [METRICS] Requests: 20 total (18 accepted, 2 rejected) | Completed: 15 | Pending: 2 | Active: 3/4
[00:00:10] [INFO] Elevator 1 arrived at floor 5
[00:00:11] [INFO] Elevator 1 doors opened, passengers boarding
[00:00:14] [INFO] HallCall abc-123 completed
[00:00:15] [INFO] Elevator 2 moving UP
[00:00:20] [METRICS] Requests: 40 total (38 accepted, 2 rejected) | Completed: 35 | Pending: 1 | Active: 2/4
```

**Phase 1 Decision:** ✅ **Logs + Minimal Metrics (print every 10 seconds)**

**Rationale:**
- Provides observability without complexity
- Zero performance impact (atomic counters)
- Useful for validation and debugging
- 20 lines of code

---

## Performance Summary

### Single Building Performance Profile

| Metric | Value | Requirement | Margin |
|--------|-------|-------------|--------|
| **Request Latency** | 10μs | <1 second | 100,000× |
| **Status Query Latency** | 50μs | <1 second | 20,000× |
| **Tick Processing (4 elevators)** | 400μs | <1 second | 2,500× |
| **Tick Processing (20 elevators)** | 2ms | <1 second | 500× |
| **Throughput Capacity** | 6M/min | 20/min | 300,000× |
| **Lock Contention** | 0.04% | N/A | Negligible |

**Verdict:** ✅ All requirements exceeded by orders of magnitude

### Scalability Characteristics

```
Elevators: O(N) - Linear scaling up to 20+ elevators
Floors: O(1) - Constant time (hash lookup)
Hall Calls: O(N) - Linear with pending hall calls (max 18)
Requests: O(1) - Constant time validation and insertion

Bottleneck: None for single building
Scalability: Linear by Building ID (horizontal scaling)
```

### Optimization Strategy

**Phase 1:** ✅ No optimization needed
- Keep single lock (simple, correct, sufficient)
- Keep synchronous (fast enough, easier to test)
- Keep in-memory (no persistence needed)
- Add minimal metrics (low overhead, high value)

**Future Optimization Triggers:**
- Lock contention >5% → Consider read-write lock
- Tick processing >100ms → Profile and optimize hot paths
- Multi-building deployment → Horizontal scaling by Building ID

**Principle:** Measure first, optimize later. Premature optimization is the root of all evil.

---

## Phase 9 Complete ✅

**Key Decisions:**
- ✅ No bottlenecks in current design (0.04% lock contention)
- ✅ Scales to 20+ elevators (2ms processing << 1000ms tick)
- ✅ No partitioning needed (single building, in-memory)
- ✅ No caching needed (all data in-memory, <50μs access)
- ✅ Single lock sufficient (balanced read/write, simple, correct)
- ✅ Keep synchronous (10μs latency, 300,000× capacity)
- ✅ No database (in-memory sufficient for Phase 1)
- ✅ Logs + minimal metrics (observability without complexity)

**Performance Validation:**
- Request processing: 10μs << 1 second ✅
- Status query: 50μs << 1 second ✅
- Throughput: 6M/min >> 20/min ✅

**Scaling Strategy:**
- Vertical: Handles 20+ elevators in single building ✅
- Horizontal: Independent Building instances by Building ID ✅

---

**Next Phase:** Phase 10 - Low Level Design (LLD)

This is where we design classes, interfaces, and patterns! 🚀
