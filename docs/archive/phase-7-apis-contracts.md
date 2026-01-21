# Phase 7 - APIs & Contracts

**Document:** APIs & Contracts  
**Status:** Complete  
**Last Updated:** January 22, 2026

---

## Overview

This phase defines all interface signatures, method contracts, preconditions, postconditions, and API specifications for the elevator system.

**Key Principles:**
- Domain interfaces: Full contracts (Option C - preconditions, postconditions)
- Simple interfaces: XML documentation (Option B)
- Return types: Result<T> pattern at service layer, exceptions in domain (Option A + C)
- Naming: PascalCase, consistent verbs (Process, Get, Can, MarkAs, Assign, Complete, Decrement)
- Async: Optional CancellationToken for future extensibility
- Value objects: Static factory methods (Of), private constructors

---

## Table of Contents

1. [Result Pattern](#result-pattern)
2. [Domain Services](#domain-services)
3. [Application Services](#application-services)
4. [Infrastructure Services](#infrastructure-services)
5. [Entity Contracts](#entity-contracts)
6. [Value Object Contracts](#value-object-contracts)
7. [Domain Events](#domain-events)
8. [Error Handling Strategy](#error-handling-strategy)

---

# Result Pattern

## Result<T> Type

```csharp
/// <summary>
/// Represents the result of an operation that can succeed or fail.
/// </summary>
/// <typeparam name="T">The type of the success value</typeparam>
public class Result<T>
{
    /// <summary>Gets whether the operation succeeded.</summary>
    public bool IsSuccess { get; }
    
    /// <summary>Gets whether the operation failed.</summary>
    public bool IsFailure => !IsSuccess;
    
    /// <summary>Gets the success value (only valid if IsSuccess is true).</summary>
    public T? Value { get; }
    
    /// <summary>Gets the error message (only valid if IsFailure is true).</summary>
    public string? Error { get; }
    
    private Result(bool isSuccess, T? value, string? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }
    
    /// <summary>Creates a successful result with a value.</summary>
    public static Result<T> Success(T value)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));
        
        return new Result<T>(true, value, null);
    }
    
    /// <summary>Creates a failed result with an error message.</summary>
    public static Result<T> Failure(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
            throw new ArgumentException("Error message cannot be empty", nameof(error));
        
        return new Result<T>(false, default, error);
    }
}

/// <summary>
/// Represents the result of an operation with no return value.
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string? Error { get; }
    
    private Result(bool isSuccess, string? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }
    
    public static Result Success() => new Result(true, null);
    public static Result Failure(string error) => new Result(false, error);
}
```

**Usage Pattern:**
```csharp
// Service returns Result
var result = await service.ProcessRequestAsync(5, 8);

// Caller checks result
if (result.IsSuccess)
{
    Console.WriteLine($"Success: Request ID = {result.Value}");
}
else
{
    Console.WriteLine($"Failed: {result.Error}");
}
```

---

# Domain Services

## IScheduler

```csharp
/// <summary>
/// Selects the optimal elevator for a hall call using direction-aware scheduling.
/// </summary>
public interface IScheduler
{
    /// <summary>
    /// Selects the best elevator to handle the specified hall call.
    /// </summary>
    /// <param name="hallCall">The hall call to assign (must not be null)</param>
    /// <param name="elevators">List of available elevators (must not be null or empty)</param>
    /// <returns>
    /// The best elevator to handle the hall call, or null if no elevator can accept it.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when hallCall or elevators is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when elevators list is empty.
    /// </exception>
    /// <remarks>
    /// <para>Preconditions:</para>
    /// <list type="bullet">
    ///   <item>hallCall != null</item>
    ///   <item>elevators != null</item>
    ///   <item>elevators.Count > 0</item>
    /// </list>
    /// <para>Postconditions:</para>
    /// <list type="bullet">
    ///   <item>Returns null OR returns elevator where CanAcceptHallCall(hallCall) == true</item>
    ///   <item>If multiple elevators can accept, returns one with lowest score</item>
    /// </list>
    /// <para>Algorithm:</para>
    /// <list type="number">
    ///   <item>Filter elevators that can accept hall call</item>
    ///   <item>Score each: IDLE/en-route = distance, wrong direction = 1000 + distance</item>
    ///   <item>Return elevator with minimum score</item>
    /// </list>
    /// </remarks>
    Elevator? SelectElevator(HallCall hallCall, List<Elevator> elevators);
}
```

---

## IElevatorMovementCoordinator

```csharp
/// <summary>
/// Determines the next action an elevator should take based on its current state.
/// </summary>
public interface IElevatorMovementCoordinator
{
    /// <summary>
    /// Decides what action the elevator should take on this tick.
    /// </summary>
    /// <param name="elevator">The elevator to evaluate (must not be null)</param>
    /// <returns>The action the elevator should execute</returns>
    /// <exception cref="ArgumentNullException">Thrown when elevator is null</exception>
    /// <remarks>
    /// <para>Preconditions:</para>
    /// <list type="bullet">
    ///   <item>elevator != null</item>
    /// </list>
    /// <para>Postconditions:</para>
    /// <list type="bullet">
    ///   <item>Returns valid ElevatorAction (never null)</item>
    ///   <item>Action is consistent with elevator's current state</item>
    /// </list>
    /// <para>Decision Logic:</para>
    /// <list type="number">
    ///   <item>If no destinations: return IDLE</item>
    ///   <item>If at destination and LOADING: return CLOSE_DOORS</item>
    ///   <item>If at destination and not LOADING: return OPEN_DOORS</item>
    ///   <item>If next destination is above: return MOVE_UP</item>
    ///   <item>If next destination is below: return MOVE_DOWN</item>
    /// </list>
    /// </remarks>
    ElevatorAction DecideNextAction(Elevator elevator);
}
```

---

## IElevatorRequestService

```csharp
/// <summary>
/// Service for handling elevator requests and status queries.
/// </summary>
public interface IElevatorRequestService
{
    /// <summary>
    /// Processes a new elevator request from a passenger.
    /// </summary>
    /// <param name="source">Source floor (0 to maxFloor-1)</param>
    /// <param name="destination">Destination floor (0 to maxFloor-1)</param>
    /// <param name="cancellationToken">Cancellation token (optional)</param>
    /// <returns>
    /// Result with request GUID if successful, or error message if failed.
    /// </returns>
    /// <remarks>
    /// <para>Preconditions:</para>
    /// <list type="bullet">
    ///   <item>0 &lt;= source &lt; maxFloor</item>
    ///   <item>0 &lt;= destination &lt; maxFloor</item>
    ///   <item>source != destination</item>
    /// </list>
    /// <para>Postconditions (on success):</para>
    /// <list type="bullet">
    ///   <item>Request created and added to building</item>
    ///   <item>HallCall created or updated</item>
    ///   <item>HallCall assigned to elevator (if available) or marked PENDING</item>
    ///   <item>Returns unique request ID</item>
    /// </list>
    /// <para>Failure Cases:</para>
    /// <list type="bullet">
    ///   <item>Invalid floor (out of range)</item>
    ///   <item>Source equals destination</item>
    ///   <item>HallCallQueue full (18 max)</item>
    /// </list>
    /// </remarks>
    Task<Result<Guid>> ProcessRequestAsync(
        int source, 
        int destination,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the current status of all elevators.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token (optional)</param>
    /// <returns>
    /// List of elevator status snapshots (immutable).
    /// </returns>
    /// <remarks>
    /// <para>Preconditions:</para>
    /// <list type="bullet">
    ///   <item>None</item>
    /// </list>
    /// <para>Postconditions:</para>
    /// <list type="bullet">
    ///   <item>Returns consistent snapshot (all read within single lock)</item>
    ///   <item>List contains exactly N elevators (e.g., 4)</item>
    ///   <item>Returned objects are immutable</item>
    /// </list>
    /// </remarks>
    Task<List<ElevatorStatus>> GetAllElevatorStatusAsync(
        CancellationToken cancellationToken = default);
}
```

---

## ITimeService

```csharp
/// <summary>
/// Abstracts time for testing (real time vs fast time).
/// </summary>
public interface ITimeService
{
    /// <summary>Gets the current UTC time.</summary>
    DateTime UtcNow { get; }
    
    /// <summary>
    /// Delays execution for the specified duration.
    /// </summary>
    /// <param name="duration">Duration to delay</param>
    /// <param name="cancellationToken">Cancellation token (optional)</param>
    Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken = default);
}
```

---

## ILogger

```csharp
/// <summary>
/// Simple logging interface for console output.
/// </summary>
public interface ILogger
{
    /// <summary>Logs an informational message.</summary>
    void LogInfo(string message);
    
    /// <summary>Logs a warning message.</summary>
    void LogWarning(string message);
    
    /// <summary>Logs an error message.</summary>
    void LogError(string message);
    
    /// <summary>Logs a debug message (optional, for detailed diagnostics).</summary>
    void LogDebug(string message);
}
```

---

# Application Services

## SystemOrchestrator

```csharp
/// <summary>
/// Orchestrates system initialization and shutdown.
/// </summary>
public class SystemOrchestrator
{
    /// <summary>
    /// Initializes and starts the elevator system.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for shutdown</param>
    /// <returns>Task that completes when system shuts down</returns>
    /// <remarks>
    /// <para>Initialization Steps:</para>
    /// <list type="number">
    ///   <item>Load SimulationConfiguration</item>
    ///   <item>Create domain services (IScheduler, IMovementCoordinator)</item>
    ///   <item>Create Building (creates 4 elevators)</item>
    ///   <item>Create IElevatorRequestService</item>
    ///   <item>Start ElevatorSimulationService</item>
    ///   <item>Start RandomRequestGenerator</item>
    /// </list>
    /// <para>Shutdown Steps:</para>
    /// <list type="number">
    ///   <item>Cancel all tasks via CancellationToken</item>
    ///   <item>Wait for tasks to complete (5 second timeout)</item>
    ///   <item>Log final statistics</item>
    /// </list>
    /// </remarks>
    public Task BootUpAsync(CancellationToken cancellationToken);
    
    /// <summary>
    /// Gracefully shuts down the system.
    /// </summary>
    public Task ShutdownAsync();
}
```

---

## ElevatorSimulationService

```csharp
/// <summary>
/// Orchestrates the simulation tick loop.
/// </summary>
public class ElevatorSimulationService
{
    /// <summary>
    /// Runs the simulation loop until cancelled.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes when simulation stops</returns>
    /// <remarks>
    /// <para>Loop Logic:</para>
    /// <list type="number">
    ///   <item>Call Building.ProcessTick()</item>
    ///   <item>Await ITimeService.DelayAsync(tickInterval)</item>
    ///   <item>Repeat until cancellation requested</item>
    /// </list>
    /// </remarks>
    public Task RunAsync(CancellationToken cancellationToken);
}
```

---

## RandomRequestGenerator

```csharp
/// <summary>
/// Generates random elevator requests for testing.
/// </summary>
public class RandomRequestGenerator
{
    /// <summary>
    /// Generates random requests until cancelled.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes when generation stops</returns>
    /// <remarks>
    /// <para>Generation Logic:</para>
    /// <list type="number">
    ///   <item>Generate random source floor (0 to maxFloor-1)</item>
    ///   <item>Generate random destination floor (0 to maxFloor-1, != source)</item>
    ///   <item>Call IElevatorRequestService.ProcessRequestAsync()</item>
    ///   <item>Await ITimeService.DelayAsync(frequency)</item>
    ///   <item>Repeat until cancellation requested</item>
    /// </list>
    /// </remarks>
    public Task GenerateAsync(CancellationToken cancellationToken);
}
```

---

# Infrastructure Services

## SimulationConfiguration

```csharp
/// <summary>
/// Configuration for the elevator simulation.
/// </summary>
public class SimulationConfiguration
{
    /// <summary>Number of floors in the building (2-100).</summary>
    public int FloorCount { get; init; }
    
    /// <summary>Number of elevators (1-10).</summary>
    public int ElevatorCount { get; init; }
    
    /// <summary>Duration doors stay open in seconds (1-60).</summary>
    public int DoorOpenDurationSeconds { get; init; }
    
    /// <summary>Simulation tick interval in milliseconds (100-10000).</summary>
    public int SimulationTickMs { get; init; }
    
    /// <summary>Request generation frequency in milliseconds (1000-60000).</summary>
    public int RequestFrequencyMs { get; init; }
    
    /// <summary>Minimum floor for random generation (0 to FloorCount-1).</summary>
    public int MinFloor { get; init; }
    
    /// <summary>Maximum floor for random generation (0 to FloorCount-1).</summary>
    public int MaxFloor { get; init; }
    
    /// <summary>
    /// Validates the configuration and throws if invalid.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if any value is out of range</exception>
    public void Validate();
    
    /// <summary>Gets default configuration values.</summary>
    public static SimulationConfiguration Default { get; }
    
    /// <summary>
    /// Loads configuration from JSON file.
    /// </summary>
    /// <param name="filePath">Path to config.json</param>
    /// <returns>Configuration loaded from file, or default if file missing</returns>
    /// <exception cref="InvalidOperationException">Thrown if file exists but is invalid</exception>
    public static SimulationConfiguration Load(string filePath);
}
```

---

# Entity Contracts

## Building (Aggregate Root)

```csharp
/// <summary>
/// Aggregate root for the elevator system.
/// </summary>
public class Building
{
    /// <summary>
    /// Processes a new elevator request.
    /// </summary>
    /// <param name="request">The request to process (must not be null)</param>
    /// <exception cref="ArgumentNullException">Thrown when request is null</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when floors are invalid or out of range
    /// </exception>
    /// <remarks>
    /// <para>Preconditions:</para>
    /// <list type="bullet">
    ///   <item>request != null</item>
    ///   <item>request.Journey.Source in [0, maxFloor)</item>
    ///   <item>request.Journey.Destination in [0, maxFloor)</item>
    /// </list>
    /// <para>Operations (within lock):</para>
    /// <list type="number">
    ///   <item>Validate request</item>
    ///   <item>GetOrCreate HallCall</item>
    ///   <item>Add destination to HallCall</item>
    ///   <item>Select elevator (if available)</item>
    ///   <item>Assign HallCall to elevator</item>
    ///   <item>Mark request as assigned</item>
    ///   <item>Publish events</item>
    /// </list>
    /// </remarks>
    public void ProcessRequest(Request request);
    
    /// <summary>
    /// Processes one simulation tick (moves elevators, completes hall calls).
    /// </summary>
    /// <remarks>
    /// <para>Operations (within lock):</para>
    /// <list type="number">
    ///   <item>Retry PENDING hall calls (FIFO order)</item>
    ///   <item>For each elevator (fixed order 1,2,3,4):
    ///     <list type="bullet">
    ///       <item>If LOADING: decrement door timer, complete hall calls if expired</item>
    ///       <item>Else: decide next action, execute action</item>
    ///     </list>
    ///   </item>
    ///   <item>Log all elevator statuses</item>
    /// </list>
    /// </remarks>
    public void ProcessTick();
    
    /// <summary>
    /// Gets the current status of all elevators.
    /// </summary>
    /// <returns>List of immutable elevator status snapshots</returns>
    /// <remarks>
    /// Acquires lock to ensure consistent snapshot.
    /// </remarks>
    public List<ElevatorStatus> GetAllElevatorStatus();
}
```

---

## Elevator

```csharp
/// <summary>
/// Represents a single elevator in the building.
/// </summary>
public class Elevator
{
    // Properties
    public int Id { get; }
    public int CurrentFloor { get; }
    public Direction Direction { get; }
    public ElevatorState State { get; }
    public bool HasPassengers { get; }
    
    /// <summary>
    /// Assigns a hall call to this elevator.
    /// </summary>
    /// <param name="hallCall">The hall call to assign (must not be null)</param>
    /// <exception cref="ArgumentNullException">Thrown when hallCall is null</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when elevator cannot accept hall call
    /// </exception>
    /// <remarks>
    /// Precondition: CanAcceptHallCall(hallCall) == true
    /// </remarks>
    public void AssignHallCall(HallCall hallCall);
    
    /// <summary>
    /// Adds a destination floor to the elevator's queue.
    /// </summary>
    /// <param name="floor">Floor to visit (0 to maxFloor-1)</param>
    /// <exception cref="ArgumentException">Thrown when floor is out of range</exception>
    public void AddDestination(int floor);
    
    /// <summary>
    /// Determines if this elevator can accept the specified hall call.
    /// </summary>
    /// <param name="hallCall">The hall call to evaluate</param>
    /// <returns>True if elevator can accept, false otherwise</returns>
    /// <remarks>
    /// <para>Rules:</para>
    /// <list type="number">
    ///   <item>If at hall call floor + LOADING: return false (already servicing)</item>
    ///   <item>If IDLE: return true</item>
    ///   <item>If MOVING + different direction: return false</item>
    ///   <item>If MOVING + same direction: 
    ///     <list type="bullet">
    ///       <item>UP: accept if current &lt; hallCallFloor &lt;= furthest</item>
    ///       <item>DOWN: accept if current &gt; hallCallFloor &gt;= furthest</item>
    ///     </list>
    ///   </item>
    /// </list>
    /// </remarks>
    public bool CanAcceptHallCall(HallCall hallCall);
    
    /// <summary>
    /// Executes the specified action.
    /// </summary>
    /// <param name="action">The action to execute</param>
    /// <remarks>
    /// <para>Actions:</para>
    /// <list type="bullet">
    ///   <item>MOVE_UP: CurrentFloor++, State=MOVING, Direction=UP</item>
    ///   <item>MOVE_DOWN: CurrentFloor--, State=MOVING, Direction=DOWN</item>
    ///   <item>OPEN_DOORS: State=LOADING, remove currentFloor from destinations, start timer</item>
    ///   <item>CLOSE_DOORS: State=MOVING or IDLE (based on destinations)</item>
    ///   <item>IDLE: No-op</item>
    /// </list>
    /// </remarks>
    public void ExecuteAction(ElevatorAction action);
    
    /// <summary>
    /// Decrements the door timer (called each tick while LOADING).
    /// </summary>
    public void DecrementDoorTimer();
    
    /// <summary>
    /// Gets whether the door timer has expired.
    /// </summary>
    public bool DoorTimerExpired { get; }
    
    /// <summary>
    /// Completes a hall call (removes from assigned list).
    /// </summary>
    /// <param name="hallCall">The hall call to complete</param>
    public void CompleteHallCall(HallCall hallCall);
}
```

---

## HallCall

```csharp
/// <summary>
/// Represents a hall call (button press at a floor).
/// </summary>
public class HallCall
{
    public Guid Id { get; }
    public int Floor { get; }
    public Direction Direction { get; }
    public DateTime CreatedAt { get; }
    public HallCallStatus Status { get; }
    public int? AssignedElevatorId { get; }
    
    /// <summary>
    /// Adds a destination to this hall call.
    /// </summary>
    /// <param name="destination">Destination floor</param>
    /// <exception cref="ArgumentException">
    /// Thrown when destination is invalid or incompatible with direction
    /// </exception>
    public void AddDestination(int destination);
    
    /// <summary>
    /// Marks this hall call as assigned to an elevator.
    /// </summary>
    /// <param name="elevatorId">ID of the assigned elevator</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when already assigned
    /// </exception>
    public void MarkAsAssigned(int elevatorId);
    
    /// <summary>
    /// Marks this hall call as completed.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when not in ASSIGNED state
    /// </exception>
    public void MarkAsCompleted();
}
```

---

## Request

```csharp
/// <summary>
/// Represents an individual passenger request.
/// </summary>
public class Request
{
    public Guid Id { get; }
    public Journey Journey { get; }
    public DateTime CreatedAt { get; }
    public RequestStatus Status { get; }
    public Guid? AssignedHallCallId { get; }
    public DateTime? CompletedAt { get; }
    public TimeSpan? WaitTime { get; }
    
    /// <summary>
    /// Creates a new request.
    /// </summary>
    /// <param name="source">Source floor</param>
    /// <param name="destination">Destination floor</param>
    /// <param name="maxFloor">Maximum floor (for validation)</param>
    /// <param name="createdAt">Creation timestamp</param>
    /// <exception cref="ArgumentException">
    /// Thrown when floors are invalid or source equals destination
    /// </exception>
    public Request(int source, int destination, int maxFloor, DateTime createdAt);
    
    /// <summary>
    /// Marks this request as assigned to a hall call.
    /// </summary>
    /// <param name="hallCallId">ID of the assigned hall call</param>
    public void MarkAsAssignedToHallCall(Guid hallCallId);
    
    /// <summary>
    /// Marks this request as completed.
    /// </summary>
    /// <param name="completedAt">Completion timestamp</param>
    public void MarkAsCompleted(DateTime completedAt);
}
```

---

# Value Object Contracts

## Journey

```csharp
/// <summary>
/// Immutable value object representing a journey from source to destination.
/// </summary>
public record Journey
{
    public int Source { get; init; }
    public int Destination { get; init; }
    public Direction Direction { get; init; }
    public int Distance => Math.Abs(Destination - Source);
    
    /// <summary>
    /// Creates a journey from source to destination.
    /// </summary>
    /// <param name="source">Source floor</param>
    /// <param name="destination">Destination floor</param>
    /// <returns>Journey instance</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when source equals destination
    /// </exception>
    public static Journey Of(int source, int destination)
    {
        if (source == destination)
            throw new ArgumentException("Source and destination cannot be the same");
        
        return new Journey
        {
            Source = source,
            Destination = destination,
            Direction = destination > source ? Direction.UP : Direction.DOWN
        };
    }
    
    // Private constructor
    private Journey() { }
}
```

---

## DestinationSet

```csharp
/// <summary>
/// Value object managing a sorted set of destination floors.
/// </summary>
public class DestinationSet
{
    public bool IsEmpty { get; }
    public int Count { get; }
    public Direction Direction { get; }
    
    /// <summary>
    /// Creates a destination set for the specified direction.
    /// </summary>
    /// <param name="direction">Direction of travel</param>
    /// <param name="maxFloor">Maximum floor for validation</param>
    public static DestinationSet Of(Direction direction, int maxFloor);
    
    /// <summary>
    /// Adds a destination floor.
    /// </summary>
    /// <param name="floor">Floor to add</param>
    /// <exception cref="ArgumentException">
    /// Thrown when floor is out of range or incompatible with direction
    /// </exception>
    public void Add(int floor);
    
    /// <summary>
    /// Removes a destination floor.
    /// </summary>
    /// <param name="floor">Floor to remove</param>
    public void Remove(int floor);
    
    /// <summary>
    /// Checks if a floor is in the destination set.
    /// </summary>
    public bool Contains(int floor);
    
    /// <summary>
    /// Gets the next destination based on current floor and direction.
    /// </summary>
    /// <param name="currentFloor">Current floor</param>
    /// <returns>Next floor to visit</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when destination set is empty
    /// </exception>
    /// <remarks>
    /// <para>Logic:</para>
    /// <list type="bullet">
    ///   <item>UP: Return min destination >= currentFloor, or max if none</item>
    ///   <item>DOWN: Return max destination &lt;= currentFloor, or min if none</item>
    ///   <item>IDLE: Return nearest destination</item>
    /// </list>
    /// <para>Critical: Use .Any() check, not != 0 (floor 0 is valid!)</para>
    /// </remarks>
    public int GetNextDestination(int currentFloor);
    
    /// <summary>
    /// Gets the furthest destination.
    /// </summary>
    /// <returns>Max floor (if UP) or min floor (if DOWN)</returns>
    public int GetFurthestDestination();
    
    /// <summary>
    /// Gets all destinations as a list.
    /// </summary>
    public List<int> GetAll();
}
```

---

## HallCallQueue

```csharp
/// <summary>
/// Value object managing hall calls with deduplication.
/// </summary>
public class HallCallQueue
{
    public int Count { get; }
    public bool IsFull => Count >= 18;
    
    /// <summary>
    /// Creates a hall call queue.
    /// </summary>
    /// <param name="maxFloor">Maximum floor for validation</param>
    /// <param name="logger">Logger for events</param>
    public static HallCallQueue Of(int maxFloor, ILogger logger);
    
    /// <summary>
    /// Gets an existing hall call or creates a new one (idempotent).
    /// </summary>
    /// <param name="floor">Floor number</param>
    /// <param name="direction">Direction (UP or DOWN)</param>
    /// <param name="timestamp">Creation timestamp</param>
    /// <returns>Existing or new hall call, or null if queue is full</returns>
    public HallCall? GetOrCreate(int floor, Direction direction, DateTime timestamp);
    
    /// <summary>
    /// Removes a hall call from the queue.
    /// </summary>
    public void Remove(HallCall hallCall);
    
    /// <summary>
    /// Gets all pending (unassigned) hall calls.
    /// </summary>
    public List<HallCall> GetPending();
    
    /// <summary>
    /// Gets all hall calls.
    /// </summary>
    public List<HallCall> GetAll();
}
```

---

## ElevatorStatus

```csharp
/// <summary>
/// Immutable snapshot of elevator state.
/// </summary>
public record ElevatorStatus
{
    public int Id { get; init; }
    public int CurrentFloor { get; init; }
    public Direction Direction { get; init; }
    public ElevatorState State { get; init; }
    public List<int> Destinations { get; init; }
    public List<Guid> AssignedHallCallIds { get; init; }
    public bool HasPassengers { get; init; }
    public DateTime Timestamp { get; init; }
    
    /// <summary>
    /// Creates an elevator status snapshot.
    /// </summary>
    public static ElevatorStatus Of(
        int id,
        int currentFloor,
        Direction direction,
        ElevatorState state,
        List<int> destinations,
        List<Guid> assignedHallCallIds,
        bool hasPassengers,
        DateTime timestamp);
    
    // Private constructor
    private ElevatorStatus() { }
}
```

---

# Domain Events

Domain events are logged but not published (no event broker in Phase 1).

## Event Definitions

```csharp
/// <summary>
/// Domain events (documented for future implementation).
/// Currently implemented as log messages only.
/// </summary>
public static class DomainEvents
{
    // Event 1: Elevator moved
    // Format: "Elevator {elevatorId} moved from floor {from} to floor {to}"
    
    // Event 2: Elevator doors opened
    // Format: "Elevator {elevatorId} doors opened at floor {floor}"
    
    // Event 3: Elevator doors closed
    // Format: "Elevator {elevatorId} doors closed at floor {floor}"
    
    // Event 4: Hall call created
    // Format: "HallCall {hallCallId} created: Floor {floor} {direction}"
    
    // Event 5: Hall call assigned
    // Format: "HallCall {hallCallId} assigned to Elevator {elevatorId}"
    
    // Event 6: Hall call completed
    // Format: "HallCall {hallCallId} completed at floor {floor} {direction}"
    
    // Event 7: Request created
    // Format: "Request {requestId} created: {source} → {destination}"
    
    // Event 8: Request fulfilled
    // Format: "Request {requestId} fulfilled, WaitTime: {waitTime}s"
}
```

**Implementation:** Events are logged via `ILogger.LogInfo()` at appropriate points.

---

# Error Handling Strategy

## Domain Layer (Throws Exceptions)

**What throws:**
- `Building.ProcessRequest()` - ArgumentException for invalid floors
- `Elevator.AddDestination()` - ArgumentException for invalid floor
- `HallCall` constructor - ArgumentException for invalid state
- `Request` constructor - ArgumentException for invalid journey
- Value object factories - ArgumentException for invalid input

**Why:** Domain enforces invariants strictly (fail-fast).

---

## Service Layer (Returns Result<T>)

**What returns Result:**
- `IElevatorRequestService.ProcessRequestAsync()` - Result<Guid>

**Pattern:**
```csharp
public async Task<Result<Guid>> ProcessRequestAsync(int source, int dest, CancellationToken ct)
{
    try
    {
        // Validate
        if (source < 0 || source >= _maxFloor)
            return Result<Guid>.Failure($"Invalid source floor: {source}");
        
        if (dest < 0 || dest >= _maxFloor)
            return Result<Guid>.Failure($"Invalid destination floor: {dest}");
        
        if (source == dest)
            return Result<Guid>.Failure("Source and destination cannot be the same");
        
        // Process (may throw from domain)
        var request = new Request(source, dest, _maxFloor, DateTime.UtcNow);
        _building.ProcessRequest(request);
        
        _logger.LogInfo($"Request processed: {request.Id}");
        return Result<Guid>.Success(request.Id);
    }
    catch (ArgumentException ex)
    {
        _logger.LogError($"Request validation failed: {ex.Message}");
        return Result<Guid>.Failure(ex.Message);
    }
    catch (Exception ex)
    {
        _logger.LogError($"Unexpected error processing request: {ex}");
        return Result<Guid>.Failure("Internal error occurred");
    }
}
```

**Why:** Services provide safe API boundary (no exceptions leak to callers).

---

## Application Layer (Checks Result)

**Pattern:**
```csharp
var result = await _requestService.ProcessRequestAsync(5, 8);

if (result.IsSuccess)
{
    Console.WriteLine($"✓ Request created: {result.Value}");
}
else
{
    Console.WriteLine($"✗ Request failed: {result.Error}");
}
```

**Why:** Application handles failures gracefully without try-catch.

---

# Summary

## Interface Design Principles

1. **Domain interfaces:** Full contracts (preconditions, postconditions, remarks)
2. **Simple interfaces:** XML documentation
3. **Return types:** Result<T> at service layer, exceptions in domain
4. **Naming:** Consistent verbs (PascalCase)
5. **Async:** Optional CancellationToken
6. **Value objects:** Static `Of()` factories, private constructors
7. **Logging:** Simple ILogger with Info/Warning/Error/Debug
8. **No versioning:** Phase 1 is internal system

## Contract Summary

| Layer | Pattern | Example |
|-------|---------|---------|
| **Domain Services** | Full contracts | IScheduler, IElevatorMovementCoordinator |
| **Application Services** | Result<T> | IElevatorRequestService |
| **Infrastructure** | Simple XML docs | ILogger, ITimeService |
| **Entities** | Exceptions | Building, Elevator |
| **Value Objects** | Static factories | Journey.Of(), DestinationSet.Of() |

## Error Handling

- Domain: Throws ArgumentException (invariant violations)
- Services: Catch and return Result<T>
- Application: Check Result.IsSuccess

## Next Phase

Phase 8: Failure Modes (What can go wrong? How to handle?)

---

**All APIs and contracts are now fully specified and ready for implementation!** ✅
