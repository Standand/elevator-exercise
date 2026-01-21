# Phase 8 - Failure Modes & Mitigation

## Overview

This document defines how the Elevator Control System handles failures, edge cases, and exceptional scenarios. Each failure mode includes detection strategy, mitigation approach, and recovery mechanism.

**Design Principle:** Fail fast during development, log and continue during operation where safe.

---

## 1. Request Validation Failures

### Scenario
Invalid request received (e.g., floor out of range, invalid direction, malformed data)

### Detection
- Validation in `Building.RequestHallCall()`
- Check floor range: `[0, MaxFloors]`
- Check direction: `UP` or `DOWN` only
- Check for duplicate hall calls (idempotency)

### Mitigation Strategy
**Reject all invalid requests immediately**

```csharp
public Result<HallCall> RequestHallCall(int floor, Direction direction)
{
    // Validation
    if (floor < 0 || floor > _maxFloors)
    {
        _logger.LogWarning($"Invalid floor: {floor}");
        _metrics.IncrementInvalidRequests();
        return Result<HallCall>.Failure($"Floor {floor} out of range [0, {_maxFloors}]");
    }
    
    if (direction != Direction.UP && direction != Direction.DOWN)
    {
        _logger.LogWarning($"Invalid direction: {direction}");
        _metrics.IncrementInvalidRequests();
        return Result<HallCall>.Failure($"Invalid direction: {direction}");
    }
    
    // Check for duplicate (idempotency)
    var existing = _hallCallQueue.FindByFloorAndDirection(floor, direction);
    if (existing != null)
    {
        _logger.LogInfo($"Duplicate hall call ignored: Floor {floor}, Direction {direction}");
        return Result<HallCall>.Success(existing); // Idempotent
    }
    
    // Valid request - proceed
    // ...
}
```

### Logging & Metrics
- ✅ **Log:** Warning level with details (floor, direction, reason)
- ✅ **Metrics:** Track `InvalidRequestsCount`
- ✅ **Response:** Return `Result.Failure()` with clear error message

---

## 2. Rate Limiting

### Scenario
Too many requests arriving in short time window

### Rate Limits
- **Global limit:** 20 requests/minute (1 request every 3 seconds)
- **Per-source limit:** 10 requests/minute per source (future-proofing for API clients)
- **Window:** Rolling 60-second window

### Detection
```csharp
public class RateLimiter
{
    private readonly int _globalLimitPerMinute = 20;
    private readonly int _perSourceLimitPerMinute = 10;
    
    private Queue<DateTime> _globalRequests = new();
    private Dictionary<string, Queue<DateTime>> _sourceRequests = new();
    private readonly object _lock = new();
    
    public bool IsAllowed(string source)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var oneMinuteAgo = now.AddMinutes(-1);
            
            // Clean old requests
            CleanOldRequests(_globalRequests, oneMinuteAgo);
            
            // Check global limit
            if (_globalRequests.Count >= _globalLimitPerMinute)
            {
                _logger.LogWarning($"Global rate limit exceeded: {_globalRequests.Count} requests in last minute");
                _metrics.IncrementRateLimitHits();
                return false;
            }
            
            // Check per-source limit
            if (!_sourceRequests.ContainsKey(source))
                _sourceRequests[source] = new Queue<DateTime>();
            
            var sourceQueue = _sourceRequests[source];
            CleanOldRequests(sourceQueue, oneMinuteAgo);
            
            if (sourceQueue.Count >= _perSourceLimitPerMinute)
            {
                _logger.LogWarning($"Per-source rate limit exceeded for '{source}': {sourceQueue.Count} requests");
                _metrics.IncrementRateLimitHits();
                return false;
            }
            
            // Allow request
            _globalRequests.Enqueue(now);
            sourceQueue.Enqueue(now);
            return true;
        }
    }
    
    private void CleanOldRequests(Queue<DateTime> queue, DateTime cutoff)
    {
        while (queue.Count > 0 && queue.Peek() < cutoff)
        {
            queue.Dequeue();
        }
    }
}
```

### Mitigation Strategy
**Reject requests exceeding rate limit**

```csharp
public Result<HallCall> RequestHallCall(int floor, Direction direction, string source = "RandomGenerator")
{
    // Rate limiting check
    if (!_rateLimiter.IsAllowed(source))
    {
        _logger.LogWarning($"Rate limit exceeded for source '{source}'");
        return Result<HallCall>.Failure("Rate limit exceeded, try again later");
    }
    
    // Proceed with validation and processing
    // ...
}
```

### Logging & Metrics
- ✅ **Log:** Warning level with source and current rate
- ✅ **Metrics:** Track `RateLimitHitsCount` (global and per-source)
- ✅ **Response:** Return `Result.Failure("Rate limit exceeded, try again later")`

### Rationale
- **20/minute global:** Matches realistic building usage (office peak: 20-30 people/minute)
- **10/minute per-source:** Prevents single client from monopolizing system
- **Prevents:** Request flood causing memory exhaustion or queue overflow
- **Allows:** 1.5x headroom over default generator (12 requests/minute)

---

## 3. Hall Call Queue Full

### Scenario
19th hall call arrives when queue is at capacity (max 18 concurrent hall calls)

### Detection
- Check `_hallCallQueue.Count` before adding
- Capacity: `MaxFloors * 2 = 10 * 2 = 20` (but practical limit is 18)

### Mitigation Strategy
**Return failure immediately (no retry)**

```csharp
public Result<HallCall> RequestHallCall(int floor, Direction direction)
{
    lock (_lock)
    {
        // ... validation ...
        
        // Check capacity
        if (_hallCallQueue.GetPendingCount() >= 18)
        {
            _logger.LogError("Hall call queue at capacity (18 pending calls)");
            _metrics.IncrementQueueFullRejections();
            return Result<HallCall>.Failure("System at capacity, try again later");
        }
        
        // Add hall call
        var hallCall = new HallCall(floor, direction);
        _hallCallQueue.Add(hallCall);
        // ...
    }
}
```

### Logging & Metrics
- ✅ **Log:** Error level (system capacity reached)
- ✅ **Metrics:** Track `QueueFullRejectionsCount`
- ✅ **Response:** Return `Result.Failure("System at capacity, try again later")`

### Recovery
- System automatically recovers as elevators complete hall calls
- No manual intervention required
- Caller can retry after delay

---

## 4. No Elevator Available

### Scenario
Hall call created but no elevator can accept it (all busy, wrong direction, etc.)

### Detection
- `Scheduler.SelectElevator()` returns `null`
- Happens during `Building.ProcessTick()` when retrying pending hall calls

### Mitigation Strategy
**Natural retry on next tick (Option A)**

```csharp
public void ProcessTick()
{
    lock (_lock)
    {
        // Step 1: Retry pending hall calls (FIFO order)
        var pendingHallCalls = _hallCallQueue.GetPending()
                                              .OrderBy(hc => hc.CreatedAt)
                                              .ToList();
        
        foreach (var hallCall in pendingHallCalls)
        {
            var elevator = _scheduler.SelectElevator(hallCall, _elevators);
            
            if (elevator != null)
            {
                elevator.AssignHallCall(hallCall);
                hallCall.MarkAsAssigned(elevator.Id);
                _logger.LogInfo($"HallCall {hallCall.Id} assigned to Elevator {elevator.Id}");
            }
            else
            {
                // Remains PENDING, will retry next tick
                _logger.LogDebug($"No elevator available for HallCall {hallCall.Id}, will retry");
            }
        }
        
        // Step 2: Process each elevator
        // ...
    }
}
```

### Logging & Metrics
- ✅ **Log:** Debug level (normal condition, will retry)
- ✅ **Metrics:** Track `PendingHallCallsCount` (gauge)
- ✅ **No timeout:** Hall call remains pending indefinitely until assigned

### Rationale
- **No timeout:** Correctness over latency - never drop valid requests
- **Natural retry:** Elevator availability changes every tick
- **FIFO fairness:** Oldest hall calls assigned first

---

## 5. Elevator Stuck in LOADING State

### Scenario
Door timer bug or logic error causes elevator to stay in LOADING state forever

### Detection
- Door timer should decrement to 0 within 3 ticks (3 seconds default)
- Safety timeout: 10 ticks (10 seconds)

### Mitigation Strategy
**Trust the timer + Add safety timeout**

```csharp
public class Elevator
{
    private int _doorTimer = 0;
    private int _loadingStateTickCount = 0; // Safety counter
    private const int SAFETY_TIMEOUT_TICKS = 10;
    
    public void ProcessTick()
    {
        if (State == ElevatorState.LOADING)
        {
            _loadingStateTickCount++;
            
            // Safety timeout check
            if (_loadingStateTickCount > SAFETY_TIMEOUT_TICKS)
            {
                _logger.LogError($"Elevator {Id} stuck in LOADING state for {_loadingStateTickCount} ticks - forcing transition");
                _metrics.IncrementSafetyTimeoutHits();
                
                // Force transition
                _doorTimer = 0;
                TransitionToNextState();
                _loadingStateTickCount = 0;
                return;
            }
            
            // Normal timer decrement
            if (_doorTimer > 0)
            {
                _doorTimer--;
                _logger.LogDebug($"Elevator {Id} door timer: {_doorTimer}");
            }
            
            if (_doorTimer == 0)
            {
                TransitionToNextState();
                _loadingStateTickCount = 0; // Reset counter
            }
        }
    }
}
```

### Logging & Metrics
- ✅ **Log:** Error level when safety timeout triggers
- ✅ **Metrics:** Track `SafetyTimeoutHitsCount` (should be 0 in production)
- ✅ **Recovery:** Force transition to next state

### Rationale
- **Trust timer:** Normal operation should work correctly
- **Safety net:** Prevents infinite hang if bug exists
- **Fail-safe:** System continues operating even with bug

---

## 6. Concurrent Request Flood

### Scenario
Multiple requests arrive simultaneously, testing thread safety

### Detection
- Multiple threads calling `Building.RequestHallCall()` concurrently
- Lock contention metrics

### Mitigation Strategy
**Single lock + Rate limiting + Validation**

```csharp
public class Building
{
    private readonly object _lock = new();
    
    public Result<HallCall> RequestHallCall(int floor, Direction direction, string source = "RandomGenerator")
    {
        lock (_lock)
        {
            // 1. Rate limiting (protects against flood)
            if (!_rateLimiter.IsAllowed(source))
            {
                return Result<HallCall>.Failure("Rate limit exceeded");
            }
            
            // 2. Validation (protects against invalid data)
            if (floor < 0 || floor > _maxFloors)
            {
                return Result<HallCall>.Failure($"Floor {floor} out of range");
            }
            
            // 3. Capacity check (protects against queue overflow)
            if (_hallCallQueue.GetPendingCount() >= 18)
            {
                return Result<HallCall>.Failure("System at capacity");
            }
            
            // 4. Idempotency check (protects against duplicates)
            var existing = _hallCallQueue.FindByFloorAndDirection(floor, direction);
            if (existing != null)
            {
                return Result<HallCall>.Success(existing);
            }
            
            // 5. Create hall call (thread-safe)
            var hallCall = new HallCall(floor, direction);
            _hallCallQueue.Add(hallCall);
            _logger.LogInfo($"HallCall {hallCall.Id} created: Floor {floor}, Direction {direction}");
            
            return Result<HallCall>.Success(hallCall);
        }
    }
}
```

### Logging & Metrics
- ✅ **Log:** Info level for successful requests, Warning for rate limits
- ✅ **Metrics:** Track `ConcurrentRequestsCount`, `LockContentionTime`
- ✅ **Thread safety:** Single lock guarantees correctness

### Rationale
- **Rate limiting:** Primary defense against flood (20/minute)
- **Single lock:** Simple, correct, sufficient for single-building simulation
- **No queue:** Synchronous processing prevents unbounded memory growth

---

## 7. Configuration File Missing

### Scenario
`appsettings.json` not found at startup

### Detection
- File I/O exception during `ConfigurationLoader.Load()`

### Mitigation Strategy
**Use defaults + Warn user**

```csharp
public class ConfigurationLoader
{
    public static SimulationConfiguration Load(string path = "appsettings.json")
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"WARNING: Configuration file '{path}' not found. Using default values.");
            return SimulationConfiguration.Default();
        }
        
        try
        {
            var json = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<SimulationConfiguration>(json);
            
            if (config == null)
            {
                Console.WriteLine($"WARNING: Failed to parse '{path}'. Using default values.");
                return SimulationConfiguration.Default();
            }
            
            return config;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WARNING: Error reading '{path}': {ex.Message}. Using default values.");
            return SimulationConfiguration.Default();
        }
    }
}

public class SimulationConfiguration
{
    public int MaxFloors { get; set; }
    public int ElevatorCount { get; set; }
    public int TickIntervalMs { get; set; }
    public int DoorOpenTicks { get; set; }
    public int RequestIntervalSeconds { get; set; }
    
    public static SimulationConfiguration Default()
    {
        return new SimulationConfiguration
        {
            MaxFloors = 10,
            ElevatorCount = 4,
            TickIntervalMs = 1000,
            DoorOpenTicks = 3,
            RequestIntervalSeconds = 5
        };
    }
}
```

### Logging & Metrics
- ✅ **Log:** Console warning (before logger initialized)
- ✅ **Defaults:** System runs with sensible defaults
- ✅ **No crash:** Simulation continues

### Default Values
```json
{
  "MaxFloors": 10,
  "ElevatorCount": 4,
  "TickIntervalMs": 1000,
  "DoorOpenTicks": 3,
  "RequestIntervalSeconds": 5
}
```

---

## 8. Configuration File Invalid

### Scenario
`appsettings.json` exists but contains invalid values (e.g., negative floors, 0 elevators)

### Detection
- Validation after deserialization

### Mitigation Strategy
**Fail fast - Force user to fix config**

```csharp
public static SimulationConfiguration Load(string path = "appsettings.json")
{
    // ... file reading ...
    
    var config = JsonSerializer.Deserialize<SimulationConfiguration>(json);
    
    // Validation
    var errors = new List<string>();
    
    if (config.MaxFloors < 2 || config.MaxFloors > 100)
        errors.Add($"MaxFloors must be between 2 and 100, got {config.MaxFloors}");
    
    if (config.ElevatorCount < 1 || config.ElevatorCount > 10)
        errors.Add($"ElevatorCount must be between 1 and 10, got {config.ElevatorCount}");
    
    if (config.TickIntervalMs < 10 || config.TickIntervalMs > 10000)
        errors.Add($"TickIntervalMs must be between 10 and 10000, got {config.TickIntervalMs}");
    
    if (config.DoorOpenTicks < 1 || config.DoorOpenTicks > 10)
        errors.Add($"DoorOpenTicks must be between 1 and 10, got {config.DoorOpenTicks}");
    
    if (config.RequestIntervalSeconds < 1 || config.RequestIntervalSeconds > 60)
        errors.Add($"RequestIntervalSeconds must be between 1 and 60, got {config.RequestIntervalSeconds}");
    
    if (errors.Any())
    {
        Console.WriteLine("ERROR: Invalid configuration:");
        foreach (var error in errors)
        {
            Console.WriteLine($"  - {error}");
        }
        Console.WriteLine($"\nPlease fix '{path}' and restart.");
        Environment.Exit(1); // Fail fast
    }
    
    return config;
}
```

### Logging & Metrics
- ✅ **Log:** Console error with all validation failures
- ✅ **Exit code:** 1 (failure)
- ✅ **No defaults:** Force user to provide valid config

### Rationale
- **Fail fast:** Invalid config indicates user error, not transient failure
- **Clear feedback:** List all validation errors at once
- **Prevents:** Silent bugs from invalid configuration

---

## 9. Simulation Loop Exception

### Scenario
Unexpected exception in `Building.ProcessTick()` (e.g., null reference, logic bug)

### Detection
- Try-catch in simulation loop

### Mitigation Strategy
**Crash - Fail fast to expose bugs during development**

```csharp
public class ElevatorSimulationService
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _logger.LogInfo("Simulation started");
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Process tick
                _building.ProcessTick();
                
                // Wait for next tick
                await Task.Delay(_tickIntervalMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown
                _logger.LogInfo("Simulation cancelled");
                break;
            }
            catch (Exception ex)
            {
                // Unexpected exception - CRASH
                _logger.LogError($"FATAL: Unexpected exception in simulation loop: {ex}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                throw; // Re-throw to crash the application
            }
        }
        
        _logger.LogInfo("Simulation stopped");
    }
}
```

### Logging & Metrics
- ✅ **Log:** Error level with full exception and stack trace
- ✅ **Crash:** Re-throw exception to terminate application
- ✅ **No retry:** Don't hide bugs

### Rationale
- **Fail fast:** Expose bugs immediately during development
- **No silent failures:** Crash is better than corrupted state
- **Debugging:** Full stack trace helps identify root cause

---

## 10. Request Generator Exception

### Scenario
Exception in `RandomRequestGenerator.GenerateAsync()` (e.g., random number generator fails)

### Detection
- Try-catch in generator loop

### Mitigation Strategy
**Log error and continue generating**

```csharp
public class RandomRequestGenerator
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _logger.LogInfo("Request generator started");
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Generate random request
                var floor = _random.Next(0, _maxFloors + 1);
                var direction = _random.Next(2) == 0 ? Direction.UP : Direction.DOWN;
                
                var result = _building.RequestHallCall(floor, direction, "RandomGenerator");
                
                if (result.IsSuccess)
                {
                    _logger.LogInfo($"Generated request: Floor {floor}, Direction {direction}");
                }
                else
                {
                    _logger.LogWarning($"Request rejected: {result.Error}");
                }
                
                // Wait for next request
                await Task.Delay(_requestIntervalMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown
                _logger.LogInfo("Request generator cancelled");
                break;
            }
            catch (Exception ex)
            {
                // Log and continue
                _logger.LogError($"Error generating request: {ex.Message}");
                _logger.LogDebug($"Stack trace: {ex.StackTrace}");
                
                // Wait before retrying
                await Task.Delay(1000, cancellationToken);
            }
        }
        
        _logger.LogInfo("Request generator stopped");
    }
}
```

### Logging & Metrics
- ✅ **Log:** Error level with exception message
- ✅ **Continue:** Don't crash entire simulation
- ✅ **Retry:** Wait 1 second before next attempt

### Rationale
- **Resilience:** Generator failure shouldn't crash simulation
- **Observability:** Log error for debugging
- **Recovery:** Automatic retry after delay

---

## 11. Graceful Shutdown Timeout

### Scenario
`Shutdown()` called but tasks don't complete within 5-second timeout

### Detection
- `Task.WaitAll()` with timeout

### Mitigation Strategy
**Log warning + Force kill after timeout**

```csharp
public class SystemOrchestrator
{
    private CancellationTokenSource _cancellationTokenSource;
    private Task _simulationTask;
    private Task _generatorTask;
    
    public void Shutdown()
    {
        _logger.LogInfo("Shutdown initiated");
        
        // Signal cancellation
        _cancellationTokenSource.Cancel();
        
        // Wait for tasks with timeout
        var tasks = new[] { _simulationTask, _generatorTask };
        var completed = Task.WaitAll(tasks, TimeSpan.FromSeconds(5));
        
        if (!completed)
        {
            _logger.LogWarning("Shutdown timeout exceeded (5 seconds)");
            _logger.LogWarning($"Simulation task completed: {_simulationTask.IsCompleted}");
            _logger.LogWarning($"Generator task completed: {_generatorTask.IsCompleted}");
            _logger.LogWarning("Forcing shutdown");
        }
        else
        {
            _logger.LogInfo("Shutdown completed gracefully");
        }
    }
}
```

### Logging & Metrics
- ✅ **Log:** Warning level with task status
- ✅ **Force kill:** Exit after timeout (don't wait forever)
- ✅ **Diagnostics:** Log which tasks are stuck

### Rationale
- **Timeout:** 5 seconds is generous for simulation cleanup
- **Force kill:** Don't hang indefinitely
- **Observability:** Log which tasks didn't complete

---

## 12. Memory Exhaustion

### Scenario
System runs for 300+ days, memory limit reached (500 MB)

### Detection
- Memory monitoring (out of scope for Phase 1)

### Mitigation Strategy
**No action for Phase 1 - Handle later**

### Rationale
- **Simulation scope:** Not designed for 300-day runs
- **Typical usage:** Hours to days, not months
- **Future work:** Add request cleanup if needed

### Memory Analysis (from Phase 6)
```
Per Request: ~200 bytes
Per Hall Call: ~150 bytes
Per Elevator: ~300 bytes

1 million requests = 200 MB
5 million requests = 1 GB (exceeds 500 MB limit)

Time to reach 5M requests:
- At 12 requests/minute: 347 days
- At 20 requests/minute: 208 days
```

**Conclusion:** Memory exhaustion not a concern for Phase 1 simulation scope.

---

## 13. Race Conditions

### Scenario
Bug in lock implementation causes race condition (e.g., double-assignment of hall call)

### Detection
- Inconsistent state (e.g., hall call assigned to 2 elevators)
- Assertion failures in tests

### Mitigation Strategy
**Trust single lock design - Simple and correct**

```csharp
public class Building
{
    private readonly object _lock = new();
    
    // ALL state mutations happen inside this lock
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

### Logging & Metrics
- ✅ **Assertions:** Add debug assertions in tests
- ✅ **Invariants:** Validate state consistency
- ✅ **Trust design:** Single lock is correct by construction

### Rationale
- **Simple:** One lock protects all state
- **Correct:** No deadlocks, no race conditions
- **Sufficient:** Performance not a concern for single building

### Invariants to Assert (in tests)
```csharp
// 1. Hall call assigned to at most 1 elevator
Assert.True(hallCall.AssignedElevatorId != null);
Assert.Single(elevators.Where(e => e.HasHallCall(hallCall.Id)));

// 2. Elevator has at most 1 active hall call per floor+direction
var hallCalls = elevator.GetAssignedHallCalls();
var duplicates = hallCalls.GroupBy(hc => (hc.Floor, hc.Direction))
                          .Where(g => g.Count() > 1);
Assert.Empty(duplicates);

// 3. All assigned hall calls exist in queue
foreach (var elevator in elevators)
{
    foreach (var hallCallId in elevator.GetAssignedHallCallIds())
    {
        Assert.NotNull(hallCallQueue.FindById(hallCallId));
    }
}
```

---

## Summary Table

| Failure Mode | Detection | Mitigation | Recovery |
|-------------|-----------|------------|----------|
| **Invalid Request** | Validation | Reject immediately | Return error |
| **Rate Limit Exceeded** | Request counter | Reject immediately | Retry after delay |
| **Queue Full** | Count check | Reject immediately | Auto-recover as queue drains |
| **No Elevator Available** | Scheduler returns null | Keep pending, retry next tick | Natural assignment |
| **Elevator Stuck** | Safety timeout (10 ticks) | Force state transition | Continue operation |
| **Concurrent Flood** | Rate limiter + lock | Reject excess requests | Process valid requests |
| **Config Missing** | File not found | Use defaults + warn | Continue with defaults |
| **Config Invalid** | Validation | Fail fast + exit | User fixes config |
| **Simulation Exception** | Try-catch | Crash immediately | Manual restart |
| **Generator Exception** | Try-catch | Log + continue | Auto-retry after 1s |
| **Shutdown Timeout** | Task.WaitAll timeout | Force kill after 5s | Exit anyway |
| **Memory Exhaustion** | N/A | No action (Phase 1) | Out of scope |
| **Race Condition** | Assertions | Trust single lock | N/A (prevented) |

---

## Error Response Format

All API methods return `Result<T>` with consistent error messages:

```csharp
// Validation errors
"Floor {floor} out of range [0, {maxFloors}]"
"Invalid direction: {direction}"

// Capacity errors
"Rate limit exceeded, try again later"
"System at capacity, try again later"

// Configuration errors
"MaxFloors must be between 2 and 100, got {value}"
"ElevatorCount must be between 1 and 10, got {value}"

// System errors
"Unexpected exception: {message}"
```

---

## Metrics to Track

```csharp
public interface IMetrics
{
    // Request metrics
    void IncrementInvalidRequests();
    void IncrementRateLimitHits();
    void IncrementQueueFullRejections();
    
    // System health
    void IncrementSafetyTimeoutHits();
    void RecordLockContentionTime(TimeSpan duration);
    
    // Gauges
    void SetPendingHallCallsCount(int count);
    void SetActiveElevatorsCount(int count);
}
```

---

## Testing Strategy

### Unit Tests
- ✅ Test each validation rule
- ✅ Test rate limiter edge cases
- ✅ Test safety timeout trigger
- ✅ Test configuration validation

### Integration Tests
- ✅ Test concurrent request flood
- ✅ Test queue full scenario
- ✅ Test no elevator available scenario
- ✅ Test graceful shutdown

### Chaos Tests
- ✅ Inject random exceptions
- ✅ Simulate config file corruption
- ✅ Simulate memory pressure
- ✅ Simulate thread contention

---

## Phase 8 Complete ✅

**Next Phase:** Phase 9 - Scalability & Performance

**Key Decisions:**
- ✅ Rate limiting: 20 requests/minute globally, 10/minute per-source
- ✅ Fail fast: Invalid config, simulation exceptions
- ✅ Log and continue: Generator exceptions
- ✅ Trust design: Single lock, no race conditions
- ✅ Safety nets: Timeout for stuck elevator, shutdown timeout
- ✅ Correctness: Never drop valid requests, retry pending hall calls
