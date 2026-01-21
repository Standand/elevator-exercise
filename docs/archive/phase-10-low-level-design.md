# Phase 10 - Low Level Design (LLD)

## Overview

This document defines the detailed class structure, design patterns, and implementation guidelines for the Elevator Control System. It translates high-level architecture into concrete C# code design.

**Design Principles:**
- Constructor injection for dependencies
- Single lock for thread safety (pessimistic locking)
- Strategy pattern for scheduling algorithms
- Factory methods for value objects
- Direct constructors for entities
- Result<T> pattern for error handling

---

## 1. Project Structure

### Directory Layout (Layer-First)

```
ElevatorSystem/
├── Domain/
│   ├── Entities/
│   │   ├── Building.cs
│   │   ├── Elevator.cs
│   │   ├── HallCall.cs
│   │   └── Request.cs
│   ├── ValueObjects/
│   │   ├── Direction.cs
│   │   ├── ElevatorState.cs
│   │   ├── HallCallStatus.cs
│   │   ├── RequestStatus.cs
│   │   ├── Journey.cs
│   │   ├── ElevatorStatus.cs
│   │   ├── DestinationSet.cs
│   │   └── HallCallQueue.cs
│   ├── Services/
│   │   ├── IScheduler.cs
│   │   ├── ISchedulingStrategy.cs
│   │   ├── DirectionAwareStrategy.cs
│   │   ├── NearestFirstStrategy.cs
│   │   ├── IElevatorMovementCoordinator.cs
│   │   └── ElevatorMovementCoordinator.cs
│   └── Events/
│       └── (Event classes - documented but not implemented)
├── Application/
│   └── Services/
│       ├── ElevatorSimulationService.cs
│       ├── RandomRequestGenerator.cs
│       └── SystemOrchestrator.cs
├── Infrastructure/
│   ├── Logging/
│   │   ├── ILogger.cs
│   │   └── ConsoleLogger.cs
│   ├── Configuration/
│   │   ├── SimulationConfiguration.cs
│   │   └── ConfigurationLoader.cs
│   ├── Time/
│   │   ├── ITimeService.cs
│   │   └── SystemTimeService.cs
│   └── Metrics/
│       ├── IMetrics.cs
│       ├── SystemMetrics.cs
│       └── MetricsSnapshot.cs
├── Common/
│   ├── Result.cs
│   └── RateLimiter.cs
└── Program.cs
```

**Rationale:**
- **Domain:** Core business logic (no dependencies on Infrastructure)
- **Application:** Orchestration and use cases
- **Infrastructure:** Technical concerns (logging, config, time)
- **Common:** Shared utilities (Result<T>, RateLimiter)

---

## 2. Design Patterns

### Pattern 1: Strategy Pattern (Scheduling Algorithms)

**Purpose:** Allow pluggable elevator selection algorithms

**Implementation:**

```csharp
// Strategy interface
public interface ISchedulingStrategy
{
    /// <summary>
    /// Selects the best elevator to service a hall call.
    /// </summary>
    /// <param name="hallCall">The hall call to service</param>
    /// <param name="elevators">Available elevators</param>
    /// <returns>Best elevator, or null if none available</returns>
    Elevator? SelectBestElevator(HallCall hallCall, List<Elevator> elevators);
}

// Concrete Strategy 1: Direction-Aware (Default)
public class DirectionAwareStrategy : ISchedulingStrategy
{
    public Elevator? SelectBestElevator(HallCall hallCall, List<Elevator> elevators)
    {
        // 1. Filter elevators that can accept this hall call
        var candidates = elevators
            .Where(e => e.CanAcceptHallCall(hallCall))
            .ToList();
        
        if (!candidates.Any())
            return null;
        
        // 2. Prioritize elevators already moving in same direction
        var sameDirection = candidates
            .Where(e => e.Direction == hallCall.Direction)
            .ToList();
        
        if (sameDirection.Any())
        {
            // Pick nearest elevator moving in same direction
            return sameDirection
                .OrderBy(e => Math.Abs(e.CurrentFloor - hallCall.Floor))
                .First();
        }
        
        // 3. Otherwise, pick nearest idle elevator
        return candidates
            .OrderBy(e => Math.Abs(e.CurrentFloor - hallCall.Floor))
            .First();
    }
}

// Concrete Strategy 2: Nearest-First (Simpler)
public class NearestFirstStrategy : ISchedulingStrategy
{
    public Elevator? SelectBestElevator(HallCall hallCall, List<Elevator> elevators)
    {
        // Ignore direction, just pick nearest available elevator
        return elevators
            .Where(e => e.State == ElevatorState.IDLE)
            .OrderBy(e => Math.Abs(e.CurrentFloor - hallCall.Floor))
            .FirstOrDefault();
    }
}

// Context (uses strategy)
public class Building
{
    private readonly ISchedulingStrategy _schedulingStrategy;
    
    public Building(
        ISchedulingStrategy schedulingStrategy,
        ILogger logger,
        IMetrics metrics,
        RateLimiter rateLimiter,
        SimulationConfiguration config)
    {
        _schedulingStrategy = schedulingStrategy ?? throw new ArgumentNullException(nameof(schedulingStrategy));
        // ...
    }
    
    private void AssignPendingHallCalls()
    {
        // Use strategy to select elevator
        var pendingHallCalls = _hallCallQueue.GetPending()
                                              .OrderBy(hc => hc.CreatedAt)
                                              .ToList();
        
        foreach (var hallCall in pendingHallCalls)
        {
            var elevator = _schedulingStrategy.SelectBestElevator(hallCall, _elevators);
            
            if (elevator != null)
            {
                elevator.AssignHallCall(hallCall);
                hallCall.MarkAsAssigned(elevator.Id);
                _logger.LogInfo($"HallCall {hallCall.Id} assigned to Elevator {elevator.Id}");
            }
        }
    }
}
```

**Benefits:**
- ✅ Open/Closed Principle (add new strategies without modifying Building)
- ✅ Easy to test strategies independently
- ✅ Easy to compare algorithms (swap strategy at startup)
- ✅ Clean separation of concerns

**Usage in Program.cs:**
```csharp
// Use DirectionAwareStrategy (default)
var strategy = new DirectionAwareStrategy();
var building = new Building(strategy, logger, metrics, rateLimiter, config);

// Or swap to NearestFirstStrategy
// var strategy = new NearestFirstStrategy();
```

---

### Pattern 2: Factory Method (Value Objects)

**Purpose:** Encapsulate validation and parsing logic for value objects

**Implementation:**

```csharp
// Direction value object
public class Direction
{
    // Singleton instances
    public static readonly Direction UP = new Direction("UP");
    public static readonly Direction DOWN = new Direction("DOWN");
    public static readonly Direction IDLE = new Direction("IDLE");
    
    public string Value { get; }
    
    // Private constructor (prevent external instantiation)
    private Direction(string value)
    {
        Value = value;
    }
    
    // Factory method (validation + parsing)
    public static Direction Of(string value)
    {
        return value.ToUpper() switch
        {
            "UP" => UP,
            "DOWN" => DOWN,
            "IDLE" => IDLE,
            _ => throw new ArgumentException($"Invalid direction: {value}")
        };
    }
    
    // Equality
    public override bool Equals(object? obj)
    {
        return obj is Direction other && Value == other.Value;
    }
    
    public override int GetHashCode() => Value.GetHashCode();
    
    public override string ToString() => Value;
    
    // Operators
    public static bool operator ==(Direction left, Direction right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        return left.Equals(right);
    }
    
    public static bool operator !=(Direction left, Direction right) => !(left == right);
}

// Journey value object (more complex)
public class Journey
{
    public int SourceFloor { get; }
    public int DestinationFloor { get; }
    public Direction Direction { get; }
    
    private Journey(int sourceFloor, int destinationFloor)
    {
        SourceFloor = sourceFloor;
        DestinationFloor = destinationFloor;
        Direction = destinationFloor > sourceFloor ? Direction.UP : Direction.DOWN;
    }
    
    // Factory method with validation
    public static Journey Of(int sourceFloor, int destinationFloor)
    {
        if (sourceFloor == destinationFloor)
            throw new ArgumentException("Source and destination floors cannot be the same");
        
        if (sourceFloor < 0 || destinationFloor < 0)
            throw new ArgumentException("Floor numbers cannot be negative");
        
        return new Journey(sourceFloor, destinationFloor);
    }
    
    public override bool Equals(object? obj)
    {
        return obj is Journey other &&
               SourceFloor == other.SourceFloor &&
               DestinationFloor == other.DestinationFloor;
    }
    
    public override int GetHashCode() => HashCode.Combine(SourceFloor, DestinationFloor);
    
    public override string ToString() => $"{SourceFloor} → {DestinationFloor} ({Direction})";
}
```

**Benefits:**
- ✅ Validation centralized in factory method
- ✅ Immutable (no setters)
- ✅ Type-safe (can't create invalid Direction)
- ✅ Singleton pattern for Direction (UP == UP reference equality)

---

### Pattern 3: Result Pattern (Error Handling)

**Purpose:** Explicit error handling without exceptions for domain operations

**Implementation:**

```csharp
// Result<T> class (reference type)
public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }
    
    private Result(bool isSuccess, T? value, string? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }
    
    public static Result<T> Success(T value)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value), "Success value cannot be null");
        
        return new Result<T>(true, value, null);
    }
    
    public static Result<T> Failure(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
            throw new ArgumentException("Error message cannot be empty", nameof(error));
        
        return new Result<T>(false, default, error);
    }
    
    // Pattern matching support
    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<string, TResult> onFailure)
    {
        return IsSuccess ? onSuccess(Value!) : onFailure(Error!);
    }
    
    public override string ToString()
    {
        return IsSuccess ? $"Success({Value})" : $"Failure({Error})";
    }
}

// Usage example
public Result<HallCall> RequestHallCall(int floor, Direction direction)
{
    lock (_lock)
    {
        // Validation
        if (floor < 0 || floor > _maxFloors)
        {
            return Result<HallCall>.Failure($"Floor {floor} out of range [0, {_maxFloors}]");
        }
        
        // Success
        var hallCall = new HallCall(floor, direction);
        _hallCallQueue.Add(hallCall);
        return Result<HallCall>.Success(hallCall);
    }
}

// Caller
var result = building.RequestHallCall(5, Direction.UP);
if (result.IsSuccess)
{
    Console.WriteLine($"Hall call created: {result.Value.Id}");
}
else
{
    Console.WriteLine($"Error: {result.Error}");
}
```

**Benefits:**
- ✅ Explicit error handling (no hidden exceptions)
- ✅ Type-safe (compiler enforces checking IsSuccess)
- ✅ Composable (can chain operations)
- ✅ Testable (easy to assert on Success/Failure)

---

## 3. Dependency Injection (Constructor Injection)

### Strategy: Manual Construction (No DI Container)

**Rationale:**
- Simple (no framework, explicit dependencies)
- Clear (can see dependency graph)
- Sufficient (only ~10 dependencies)
- Educational (shows understanding without crutches)

### Dependency Graph

```
SystemOrchestrator
├── ElevatorSimulationService
│   ├── Building
│   │   ├── ISchedulingStrategy (DirectionAwareStrategy)
│   │   ├── ILogger (ConsoleLogger)
│   │   ├── IMetrics (SystemMetrics)
│   │   ├── RateLimiter
│   │   └── SimulationConfiguration
│   ├── ITimeService (SystemTimeService)
│   ├── ILogger (ConsoleLogger)
│   └── SimulationConfiguration
├── RandomRequestGenerator
│   ├── Building
│   ├── ILogger (ConsoleLogger)
│   └── SimulationConfiguration
└── ILogger (ConsoleLogger)
```

### Implementation Example

```csharp
// Building.cs (Domain Entity)
public class Building
{
    private readonly ISchedulingStrategy _schedulingStrategy;
    private readonly ILogger _logger;
    private readonly IMetrics _metrics;
    private readonly RateLimiter _rateLimiter;
    private readonly int _maxFloors;
    private readonly object _lock = new();
    
    // Constructor injection
    public Building(
        ISchedulingStrategy schedulingStrategy,
        ILogger logger,
        IMetrics metrics,
        RateLimiter rateLimiter,
        SimulationConfiguration config)
    {
        _schedulingStrategy = schedulingStrategy ?? throw new ArgumentNullException(nameof(schedulingStrategy));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
        
        _maxFloors = config.MaxFloors;
        
        // Initialize elevators
        _elevators = new List<Elevator>();
        for (int i = 1; i <= config.ElevatorCount; i++)
        {
            _elevators.Add(new Elevator(i, config.MaxFloors, config.DoorOpenTicks, logger));
        }
        
        _hallCallQueue = new HallCallQueue();
    }
    
    // ... methods ...
}

// ElevatorSimulationService.cs (Application Service)
public class ElevatorSimulationService
{
    private readonly Building _building;
    private readonly ITimeService _timeService;
    private readonly ILogger _logger;
    private readonly int _tickIntervalMs;
    
    public ElevatorSimulationService(
        Building building,
        ITimeService timeService,
        ILogger logger,
        SimulationConfiguration config)
    {
        _building = building ?? throw new ArgumentNullException(nameof(building));
        _timeService = timeService ?? throw new ArgumentNullException(nameof(timeService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tickIntervalMs = config.TickIntervalMs;
    }
    
    // ... methods ...
}
```

**Benefits:**
- ✅ Explicit dependencies (visible in constructor)
- ✅ Immutable after construction (thread-safe)
- ✅ Null checks (fail fast if dependency missing)
- ✅ Easy to test (pass mocks in constructor)

---

## 4. Concurrency Design (Pessimistic Locking)

### Strategy: Single Lock, All Public Methods

**Rule:** Lock every public method that accesses mutable state.

**Implementation:**

```csharp
public class Building
{
    private readonly object _lock = new();
    
    // Mutable state (protected by lock)
    private readonly List<Elevator> _elevators;
    private readonly HallCallQueue _hallCallQueue;
    
    // PUBLIC METHODS: Always lock
    
    public Result<HallCall> RequestHallCall(int floor, Direction direction, string source = "RandomGenerator")
    {
        lock (_lock)  // LOCK
        {
            // Rate limiting
            if (!_rateLimiter.IsAllowed(source))
            {
                _logger.LogWarning($"Rate limit exceeded for source '{source}'");
                _metrics.IncrementRateLimitHits();
                return Result<HallCall>.Failure("Rate limit exceeded, try again later");
            }
            
            // Validation
            if (floor < 0 || floor > _maxFloors)
            {
                _logger.LogWarning($"Invalid floor: {floor}");
                _metrics.IncrementInvalidRequests();
                return Result<HallCall>.Failure($"Floor {floor} out of range [0, {_maxFloors}]");
            }
            
            // Idempotency check
            var existing = _hallCallQueue.FindByFloorAndDirection(floor, direction);
            if (existing != null)
            {
                _logger.LogInfo($"Duplicate hall call ignored: Floor {floor}, Direction {direction}");
                return Result<HallCall>.Success(existing);
            }
            
            // Capacity check
            if (_hallCallQueue.GetPendingCount() >= _maxFloors * 2 - 2)
            {
                _logger.LogError("Hall call queue at capacity");
                _metrics.IncrementQueueFullRejections();
                return Result<HallCall>.Failure("System at capacity, try again later");
            }
            
            // Create hall call
            var hallCall = new HallCall(floor, direction);
            _hallCallQueue.Add(hallCall);
            _metrics.IncrementTotalRequests();
            _logger.LogInfo($"HallCall {hallCall.Id} created: Floor {floor}, Direction {direction}");
            
            return Result<HallCall>.Success(hallCall);
        }
    }
    
    public void ProcessTick()
    {
        lock (_lock)  // LOCK
        {
            // Step 1: Retry pending hall calls
            AssignPendingHallCalls();
            
            // Step 2: Process each elevator
            foreach (var elevator in _elevators)
            {
                elevator.ProcessTick();
            }
            
            // Step 3: Update metrics
            _metrics.SetPendingHallCallsCount(_hallCallQueue.GetPendingCount());
            _metrics.SetActiveElevatorsCount(_elevators.Count(e => e.State != ElevatorState.IDLE));
        }
    }
    
    public BuildingStatus GetStatus()
    {
        lock (_lock)  // LOCK
        {
            return new BuildingStatus
            {
                Elevators = _elevators.Select(e => e.GetStatus()).ToList(),
                PendingHallCalls = _hallCallQueue.GetPending().Select(hc => hc.GetStatus()).ToList(),
                Timestamp = DateTime.UtcNow
            };
        }
    }
    
    // PRIVATE METHODS: Assume lock is held
    
    private void AssignPendingHallCalls()
    {
        // No lock here - assumes caller (ProcessTick) holds lock
        
        var pendingHallCalls = _hallCallQueue.GetPending()
                                              .OrderBy(hc => hc.CreatedAt)
                                              .ToList();
        
        foreach (var hallCall in pendingHallCalls)
        {
            var elevator = _schedulingStrategy.SelectBestElevator(hallCall, _elevators);
            
            if (elevator != null)
            {
                elevator.AssignHallCall(hallCall);
                hallCall.MarkAsAssigned(elevator.Id);
                _logger.LogInfo($"HallCall {hallCall.Id} assigned to Elevator {elevator.Id}");
            }
            else
            {
                _logger.LogDebug($"No elevator available for HallCall {hallCall.Id}, will retry");
            }
        }
    }
}
```

**Lock Discipline:**
- ✅ **Public methods:** Always acquire lock
- ✅ **Private methods:** Assume lock is held (document with comment)
- ✅ **No nested locks:** Single lock eliminates deadlock risk
- ✅ **Short critical sections:** Lock held for <1ms

**Benefits:**
- ✅ Correct (no race conditions)
- ✅ Simple (one lock, easy to reason about)
- ✅ Sufficient (0.04% contention, no performance issue)

---

## 5. Configuration Management

### Strategy: Throw Exceptions on Invalid Config

**Rationale:**
- Invalid configuration is exceptional (not normal flow)
- Fail fast at startup (don't run with bad config)
- Standard practice in .NET ecosystem

**Implementation:**

```csharp
// SimulationConfiguration.cs
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

// ConfigurationLoader.cs
public static class ConfigurationLoader
{
    public static SimulationConfiguration Load(string path = "appsettings.json")
    {
        // File missing - use defaults
        if (!File.Exists(path))
        {
            Console.WriteLine($"WARNING: Configuration file '{path}' not found. Using default values.");
            return SimulationConfiguration.Default();
        }
        
        try
        {
            // Parse JSON
            var json = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<SimulationConfiguration>(json);
            
            if (config == null)
            {
                Console.WriteLine($"WARNING: Failed to parse '{path}'. Using default values.");
                return SimulationConfiguration.Default();
            }
            
            // Validate (throws on invalid)
            Validate(config);
            
            return config;
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"WARNING: Error parsing '{path}': {ex.Message}. Using default values.");
            return SimulationConfiguration.Default();
        }
        catch (ArgumentException ex)
        {
            // Validation failed - FAIL FAST
            Console.WriteLine($"ERROR: Invalid configuration in '{path}':");
            Console.WriteLine($"  {ex.Message}");
            Console.WriteLine("\nPlease fix the configuration file and restart.");
            Environment.Exit(1);
            return null;  // Unreachable
        }
    }
    
    private static void Validate(SimulationConfiguration config)
    {
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
            throw new ArgumentException(string.Join("\n  ", errors));
        }
    }
}
```

**Error Handling:**
- ✅ File missing → Use defaults + warn
- ✅ Parse error → Use defaults + warn
- ✅ Validation error → Fail fast + exit

---

## 6. Application Entry Point (Program.cs)

### Strategy: Manual Dependency Construction

**Implementation:**

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.Services;
using ElevatorSystem.Application.Services;
using ElevatorSystem.Infrastructure.Logging;
using ElevatorSystem.Infrastructure.Configuration;
using ElevatorSystem.Infrastructure.Time;
using ElevatorSystem.Infrastructure.Metrics;
using ElevatorSystem.Common;

namespace ElevatorSystem
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("=== Elevator Control System ===\n");
            
            // Step 1: Load configuration
            var config = ConfigurationLoader.Load("appsettings.json");
            Console.WriteLine($"Configuration loaded: {config.ElevatorCount} elevators, {config.MaxFloors} floors\n");
            
            // Step 2: Create infrastructure dependencies
            var logger = new ConsoleLogger();
            var timeService = new SystemTimeService();
            var metrics = new SystemMetrics();
            var rateLimiter = new RateLimiter(
                globalLimitPerMinute: 20,
                perSourceLimitPerMinute: 10,
                logger);
            
            // Step 3: Create domain services
            var schedulingStrategy = new DirectionAwareStrategy();
            
            // Step 4: Create building (domain entity)
            var building = new Building(
                schedulingStrategy,
                logger,
                metrics,
                rateLimiter,
                config);
            
            // Step 5: Create application services
            var simulationService = new ElevatorSimulationService(
                building,
                timeService,
                logger,
                config);
            
            var requestGenerator = new RandomRequestGenerator(
                building,
                logger,
                config);
            
            // Step 6: Create orchestrator
            var orchestrator = new SystemOrchestrator(
                simulationService,
                requestGenerator,
                metrics,
                logger);
            
            // Step 7: Setup graceful shutdown
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                Console.WriteLine("\n\nShutdown requested (Ctrl+C)...");
                orchestrator.Shutdown();
                cts.Cancel();
                e.Cancel = true;  // Prevent immediate termination
            };
            
            // Step 8: Start system
            await orchestrator.StartAsync(cts.Token);
            
            Console.WriteLine("\nSystem started. Press Ctrl+C to stop.\n");
            
            // Step 9: Wait for shutdown
            try
            {
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (TaskCanceledException)
            {
                // Expected on Ctrl+C
            }
            
            Console.WriteLine("System stopped.");
        }
    }
}
```

**Dependency Construction Order:**
1. Configuration (no dependencies)
2. Infrastructure (logger, time, metrics, rate limiter)
3. Domain services (scheduling strategy)
4. Domain entities (building)
5. Application services (simulation, generator)
6. Orchestrator (coordinates everything)

**Benefits:**
- ✅ Explicit (can see entire dependency graph)
- ✅ Simple (no DI framework magic)
- ✅ Debuggable (straightforward stack traces)
- ✅ Educational (shows understanding of dependencies)

---

## 7. Key Class Designs

### 7.1 Building (Domain Entity)

```csharp
public class Building
{
    // Dependencies (injected)
    private readonly ISchedulingStrategy _schedulingStrategy;
    private readonly ILogger _logger;
    private readonly IMetrics _metrics;
    private readonly RateLimiter _rateLimiter;
    
    // Configuration
    private readonly int _maxFloors;
    
    // State (protected by lock)
    private readonly object _lock = new();
    private readonly List<Elevator> _elevators;
    private readonly HallCallQueue _hallCallQueue;
    
    // Constructor
    public Building(
        ISchedulingStrategy schedulingStrategy,
        ILogger logger,
        IMetrics metrics,
        RateLimiter rateLimiter,
        SimulationConfiguration config)
    {
        _schedulingStrategy = schedulingStrategy ?? throw new ArgumentNullException(nameof(schedulingStrategy));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
        
        _maxFloors = config.MaxFloors;
        
        // Initialize elevators
        _elevators = new List<Elevator>();
        for (int i = 1; i <= config.ElevatorCount; i++)
        {
            _elevators.Add(new Elevator(i, config.MaxFloors, config.DoorOpenTicks, logger));
        }
        
        _hallCallQueue = new HallCallQueue();
        
        _logger.LogInfo($"Building initialized: {config.ElevatorCount} elevators, {config.MaxFloors} floors");
    }
    
    // Public API
    public Result<HallCall> RequestHallCall(int floor, Direction direction, string source = "RandomGenerator")
    {
        lock (_lock) { /* ... implementation from Phase 7 ... */ }
    }
    
    public void ProcessTick()
    {
        lock (_lock) { /* ... implementation from Phase 5 ... */ }
    }
    
    public BuildingStatus GetStatus()
    {
        lock (_lock) { /* ... implementation from Phase 7 ... */ }
    }
    
    // Private helpers
    private void AssignPendingHallCalls()
    {
        // Assumes lock is held
        // ... implementation from Phase 5 ...
    }
}
```

---

### 7.2 Elevator (Domain Entity)

```csharp
public class Elevator
{
    // Identity
    public int Id { get; }
    
    // Dependencies
    private readonly ILogger _logger;
    
    // Configuration
    private readonly int _maxFloors;
    private readonly int _doorOpenDuration;
    
    // State (mutable, but protected by Building's lock)
    public int CurrentFloor { get; private set; }
    public Direction Direction { get; private set; }
    public ElevatorState State { get; private set; }
    
    private readonly DestinationSet _destinations;
    private readonly List<Guid> _assignedHallCallIds;
    private int _doorTimer;
    private int _loadingStateTickCount;
    
    // Constructor
    public Elevator(int id, int maxFloors, int doorOpenDuration, ILogger logger)
    {
        Id = id;
        _maxFloors = maxFloors;
        _doorOpenDuration = doorOpenDuration;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Initial state
        CurrentFloor = 0;
        Direction = Direction.IDLE;
        State = ElevatorState.IDLE;
        _destinations = new DestinationSet(Direction.IDLE);
        _assignedHallCallIds = new List<Guid>();
        _doorTimer = 0;
        _loadingStateTickCount = 0;
    }
    
    // Public methods (called by Building, which holds lock)
    public bool CanAcceptHallCall(HallCall hallCall)
    {
        // ... implementation from Phase 4 ...
    }
    
    public void AssignHallCall(HallCall hallCall)
    {
        _assignedHallCallIds.Add(hallCall.Id);
        _destinations.Add(hallCall.Floor);
        _logger.LogInfo($"Elevator {Id} assigned HallCall {hallCall.Id} (Floor {hallCall.Floor})");
    }
    
    public void AddDestination(int floor)
    {
        _destinations.Add(floor);
        _logger.LogInfo($"Elevator {Id} destination added: Floor {floor}");
    }
    
    public void ProcessTick()
    {
        // ... implementation from Phase 6 ...
    }
    
    public ElevatorStatus GetStatus()
    {
        return new ElevatorStatus
        {
            Id = Id,
            CurrentFloor = CurrentFloor,
            Direction = Direction,
            State = State,
            Destinations = _destinations.GetAll(),
            AssignedHallCallIds = _assignedHallCallIds.ToList()
        };
    }
    
    // Private helpers
    private void TransitionToNextState()
    {
        // ... implementation from Phase 6 ...
    }
}
```

---

### 7.3 SystemOrchestrator (Application Service)

```csharp
public class SystemOrchestrator
{
    private readonly ElevatorSimulationService _simulationService;
    private readonly RandomRequestGenerator _requestGenerator;
    private readonly IMetrics _metrics;
    private readonly ILogger _logger;
    
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _simulationTask;
    private Task? _generatorTask;
    private Task? _metricsTask;
    
    public SystemOrchestrator(
        ElevatorSimulationService simulationService,
        RandomRequestGenerator requestGenerator,
        IMetrics metrics,
        ILogger logger)
    {
        _simulationService = simulationService ?? throw new ArgumentNullException(nameof(simulationService));
        _requestGenerator = requestGenerator ?? throw new ArgumentNullException(nameof(requestGenerator));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _cancellationTokenSource.Token;
        
        _logger.LogInfo("Starting system...");
        
        // Start simulation loop
        _simulationTask = _simulationService.RunAsync(token);
        
        // Start request generator
        _generatorTask = _requestGenerator.RunAsync(token);
        
        // Start metrics reporter (print every 10 seconds)
        _metricsTask = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(10000, token);
                
                var snapshot = _metrics.GetSnapshot();
                _logger.LogInfo($"[METRICS] Requests: {snapshot.TotalRequests} total " +
                               $"({snapshot.AcceptedRequests} accepted, {snapshot.RejectedRequests} rejected) | " +
                               $"Completed: {snapshot.CompletedHallCalls} | " +
                               $"Pending: {snapshot.PendingHallCalls} | " +
                               $"Active Elevators: {snapshot.ActiveElevators}/{snapshot.ActiveElevators + snapshot.IdleElevators}");
            }
        }, token);
        
        _logger.LogInfo("System started");
    }
    
    public void Shutdown()
    {
        _logger.LogInfo("Shutdown initiated");
        
        // Signal cancellation
        _cancellationTokenSource?.Cancel();
        
        // Wait for tasks with timeout
        var tasks = new[] { _simulationTask, _generatorTask, _metricsTask }
                        .Where(t => t != null)
                        .ToArray();
        
        var completed = Task.WaitAll(tasks, TimeSpan.FromSeconds(5));
        
        if (!completed)
        {
            _logger.LogWarning("Shutdown timeout exceeded (5 seconds)");
            _logger.LogWarning($"Simulation task completed: {_simulationTask?.IsCompleted}");
            _logger.LogWarning($"Generator task completed: {_generatorTask?.IsCompleted}");
            _logger.LogWarning($"Metrics task completed: {_metricsTask?.IsCompleted}");
            _logger.LogWarning("Forcing shutdown");
        }
        else
        {
            _logger.LogInfo("Shutdown completed gracefully");
        }
    }
}
```

---

## 8. Interface Definitions

### 8.1 Domain Services

```csharp
// ISchedulingStrategy.cs
public interface ISchedulingStrategy
{
    /// <summary>
    /// Selects the best elevator to service a hall call.
    /// </summary>
    /// <param name="hallCall">The hall call to service</param>
    /// <param name="elevators">Available elevators</param>
    /// <returns>Best elevator, or null if none available</returns>
    Elevator? SelectBestElevator(HallCall hallCall, List<Elevator> elevators);
}
```

### 8.2 Infrastructure Services

```csharp
// ILogger.cs
public interface ILogger
{
    void LogDebug(string message);
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message);
}

// ITimeService.cs
public interface ITimeService
{
    DateTime GetCurrentTime();
}

// IMetrics.cs
public interface IMetrics
{
    void IncrementTotalRequests();
    void IncrementAcceptedRequests();
    void IncrementRejectedRequests();
    void IncrementCompletedHallCalls();
    void IncrementRateLimitHits();
    void IncrementQueueFullRejections();
    void IncrementSafetyTimeoutHits();
    
    void SetPendingHallCallsCount(int count);
    void SetActiveElevatorsCount(int count);
    
    MetricsSnapshot GetSnapshot();
}
```

---

## 9. Testing Strategy

### Unit Tests (Per Class)

```csharp
// Example: DirectionAwareStrategy tests
[TestClass]
public class DirectionAwareStrategyTests
{
    [TestMethod]
    public void SelectBestElevator_IdleElevator_SelectsNearest()
    {
        // Arrange
        var strategy = new DirectionAwareStrategy();
        var logger = new MockLogger();
        
        var elevator1 = new Elevator(1, 10, 3, logger) { CurrentFloor = 0, State = IDLE };
        var elevator2 = new Elevator(2, 10, 3, logger) { CurrentFloor = 8, State = IDLE };
        var elevators = new List<Elevator> { elevator1, elevator2 };
        
        var hallCall = new HallCall(5, Direction.UP);
        
        // Act
        var result = strategy.SelectBestElevator(hallCall, elevators);
        
        // Assert
        Assert.AreEqual(1, result.Id);  // Elevator 1 is closer (5 floors vs 3 floors)
    }
    
    [TestMethod]
    public void SelectBestElevator_NoAvailableElevators_ReturnsNull()
    {
        // Arrange
        var strategy = new DirectionAwareStrategy();
        var logger = new MockLogger();
        
        var elevator1 = new Elevator(1, 10, 3, logger) { CurrentFloor = 0, Direction = DOWN, State = MOVING };
        var elevators = new List<Elevator> { elevator1 };
        
        var hallCall = new HallCall(5, Direction.UP);
        
        // Act
        var result = strategy.SelectBestElevator(hallCall, elevators);
        
        // Assert
        Assert.IsNull(result);
    }
}

// Example: Building tests
[TestClass]
public class BuildingTests
{
    [TestMethod]
    public void RequestHallCall_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var building = CreateBuilding();
        
        // Act
        var result = building.RequestHallCall(5, Direction.UP);
        
        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Value);
        Assert.AreEqual(5, result.Value.Floor);
    }
    
    [TestMethod]
    public void RequestHallCall_InvalidFloor_ReturnsFailure()
    {
        // Arrange
        var building = CreateBuilding();
        
        // Act
        var result = building.RequestHallCall(15, Direction.UP);  // Max floors = 10
        
        // Assert
        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.Error.Contains("out of range"));
    }
    
    [TestMethod]
    public void RequestHallCall_RateLimitExceeded_ReturnsFailure()
    {
        // Arrange
        var building = CreateBuilding();
        
        // Flood with requests
        for (int i = 0; i < 25; i++)
        {
            building.RequestHallCall(i % 10, Direction.UP);
        }
        
        // Act (21st request should be rate limited)
        var result = building.RequestHallCall(5, Direction.UP);
        
        // Assert
        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.Error.Contains("Rate limit exceeded"));
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
        
        var logger = new MockLogger();
        var metrics = new MockMetrics();
        var rateLimiter = new RateLimiter(20, 10, logger);
        var strategy = new DirectionAwareStrategy();
        
        return new Building(strategy, logger, metrics, rateLimiter, config);
    }
}
```

### Integration Tests (End-to-End)

```csharp
[TestClass]
public class ElevatorSystemIntegrationTests
{
    [TestMethod]
    public async Task FullSimulation_10Requests_AllCompleted()
    {
        // Arrange
        var config = new SimulationConfiguration
        {
            MaxFloors = 10,
            ElevatorCount = 4,
            TickIntervalMs = 100,  // Fast for testing
            DoorOpenTicks = 3,
            RequestIntervalSeconds = 1
        };
        
        var building = CreateBuilding(config);
        var simulation = new ElevatorSimulationService(building, new SystemTimeService(), new MockLogger(), config);
        
        // Act: Generate 10 requests
        for (int i = 0; i < 10; i++)
        {
            building.RequestHallCall(i % 10, Direction.UP);
        }
        
        // Run simulation for 30 seconds
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await simulation.RunAsync(cts.Token);
        
        // Assert: All requests completed
        var status = building.GetStatus();
        Assert.AreEqual(0, status.PendingHallCalls.Count);
    }
}
```

---

## 10. Code Style Guidelines

### Naming Conventions

- **Classes:** PascalCase (`Building`, `Elevator`, `DirectionAwareStrategy`)
- **Interfaces:** IPascalCase (`ILogger`, `ISchedulingStrategy`)
- **Methods:** PascalCase (`RequestHallCall`, `ProcessTick`)
- **Private fields:** _camelCase (`_logger`, `_schedulingStrategy`)
- **Parameters:** camelCase (`hallCall`, `elevators`)
- **Constants:** UPPER_SNAKE_CASE or PascalCase (`MAX_FLOORS`, `DefaultTickInterval`)

### File Organization

```csharp
// 1. Usings (sorted)
using System;
using System.Collections.Generic;
using System.Linq;

// 2. Namespace
namespace ElevatorSystem.Domain.Entities
{
    // 3. Class documentation
    /// <summary>
    /// Represents a building with multiple elevators.
    /// </summary>
    public class Building
    {
        // 4. Fields (grouped by type)
        // Dependencies
        private readonly ISchedulingStrategy _schedulingStrategy;
        private readonly ILogger _logger;
        
        // Configuration
        private readonly int _maxFloors;
        
        // State
        private readonly object _lock = new();
        private readonly List<Elevator> _elevators;
        
        // 5. Constructor
        public Building(/* ... */) { }
        
        // 6. Public methods
        public Result<HallCall> RequestHallCall(/* ... */) { }
        
        // 7. Private methods
        private void AssignPendingHallCalls() { }
    }
}
```

### Comments

```csharp
// Good: Explain WHY, not WHAT
// Rule 1: If at the hall call floor in LOADING state, CANNOT accept
// (Elevator already there, don't accept duplicate calls)
if (CurrentFloor == hallCall.Floor && State == LOADING)
{
    return false;
}

// Bad: Redundant comment
// Check if current floor equals hall call floor
if (CurrentFloor == hallCall.Floor)
{
    return false;
}
```

---

## Phase 10 Complete ✅

**Key Decisions:**
- ✅ Layer-first directory structure (Domain, Application, Infrastructure)
- ✅ Constructor injection (explicit dependencies)
- ✅ Strategy pattern for scheduling (pluggable algorithms)
- ✅ Factory methods for value objects (validation + immutability)
- ✅ Result<T> as class (idiomatic C#)
- ✅ Pessimistic locking (lock all public methods)
- ✅ Configuration validation throws exceptions (fail fast)
- ✅ Manual dependency construction (no DI container)

**Design Patterns Used:**
1. **Strategy Pattern:** Scheduling algorithms
2. **Factory Method:** Value object creation
3. **Result Pattern:** Error handling
4. **Dependency Injection:** Constructor injection

**Next Phase:** Phase 11 - Code Implementation

Ready to start coding! 🚀
