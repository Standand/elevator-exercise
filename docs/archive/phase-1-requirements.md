# Phase 1 - Requirements

## Functional Requirements

### 1. Actors

**Primary Actors:**
- **Random Request Generator**: Automated component generating simulated elevator requests
- **Simulation Consumers**: Running and observing the simulation

**Future Actors (Extensibility):**
- External systems via REST API
- Monitoring systems (read-only status queries)

**Out of Scope:**
- Admin users with runtime configuration
- End-user mobile apps
- Multi-tenant building management systems

---

### 2. Actions

The system must perform the following actions:

**Request Management:**
- Receive elevator request (source floor + destination floor)
- Validate request (floor range, destination ≠ source)
- Queue pending requests
- Assign request to optimal elevator

**Elevator Operations:**
- Move elevator between floors (one floor at a time)
- Stop elevator at target floor
- Load passengers (simulated with time delay)
- Unload passengers (simulated with time delay)
- Update elevator state (IDLE → MOVING → STOPPED → LOADING → IDLE)
- Maintain destination queue per elevator

**Request Generation:**
- Generate random requests at configured frequency
- Generate valid source and destination floors
- Derive direction from source/destination

**Status & Observability:**
- Query current status of all elevators
- Query status of specific elevator
- Log all state changes
- Display elevator positions in real-time

**Control Operations:**
- Start simulation
- Stop simulation
- Graceful shutdown

**Out of Scope Actions:**
- Pause/resume simulation
- Runtime configuration changes
- Handle stuck/malfunctioning elevators
- Emergency protocols
- Maintenance mode

---

### 3. System Inputs

**Configuration File (JSON):**
```json
{
  "building": {
    "floors": 10,
    "numberOfElevators": 4,
    "initialFloor": 1
  },
  "timing": {
    "movementTimeMs": 10000,
    "loadingTimeMs": 10000,
    "testMode": {
      "movementTimeMs": 100,
      "loadingTimeMs": 100
    }
  },
  "requestGenerator": {
    "enabled": true,
    "frequencyMs": 5000,
    "minFloor": 1,
    "maxFloor": 10
  }
}
```

**Default values documented in Phase 2 Constraints.**

**Control Commands:**
- START - Begin simulation
- STOP - Graceful shutdown

**Request Input (from generator):**
- Source floor (1-10)
- Destination floor (1-10, must be ≠ source)
- Direction (derived from source/destination)

**Request Processing Model (Idempotency):**
- **External Interface:** Complete journey (source + destination)
- **Internal Representation:** Hall calls deduplicated by (floor, direction)
- **Behavior:** Multiple requests for same (floor, direction) are merged
  - Request 1: Floor 5 → Floor 8 (UP) creates HallCall(5, UP) with destination [8]
  - Request 2: Floor 5 → Floor 10 (UP) updates HallCall(5, UP) with destinations [8, 10]
  - Elevator serves both destinations in one trip
- **Max Concurrent Hall Calls:** 18 unique (floor, direction) combinations
  - Floor 1: 1 hall call (UP only)
  - Floor 10: 1 hall call (DOWN only)
  - Floors 2-9: 16 hall calls (8 floors × 2 directions)

**Validation Rules:**
- Source floor must be in range [1, floors]
- Destination floor must be in range [1, floors]
- Destination ≠ source floor
- Floor numbers must be valid integers
- Direction must match source/destination relationship (auto-derived)

---

### 4. System Outputs

**Console Logging (Primary Output):**

**Elevator Status Display (Updated regularly):**
```
=== ELEVATOR STATUS @ 2024-01-20 10:30:15 ===
Elevator 1 | Floor: 3  | Direction: UP   | Passengers: YES | Stops: [5, 7, 9]
Elevator 2 | Floor: 8  | Direction: DOWN | Passengers: NO  | Stops: []
Elevator 3 | Floor: 1  | Direction: IDLE | Passengers: NO  | Stops: []
Elevator 4 | Floor: 10 | Direction: DOWN | Passengers: YES | Stops: [6, 3, 1]

Pending Requests: 3 | Completed: 127 | Average Wait: 23.5s
```

**Event Logging:**
```
[10:30:15.123] [INFO] [REQUEST] REQ-001 created: Floor 7 → Floor 10 (UP)
[10:30:15.145] [INFO] [ASSIGNMENT] REQ-001 assigned to Elevator 2
[10:30:15.150] [INFO] [STATE] Elevator 2: IDLE → MOVING
[10:30:25.152] [INFO] [MOVEMENT] Elevator 2: Floor 5 → Floor 6
[10:30:35.154] [INFO] [MOVEMENT] Elevator 2: Floor 6 → Floor 7
[10:30:36.155] [INFO] [STATE] Elevator 2: MOVING → STOPPED
[10:30:36.156] [INFO] [EVENT] Elevator 2 arrived at floor 7
[10:30:36.157] [INFO] [STATE] Elevator 2: STOPPED → LOADING
[10:30:46.158] [INFO] [EVENT] Passengers boarded at floor 7, destination floor 10
[10:30:46.159] [INFO] [STATE] Elevator 2: LOADING → MOVING
```

**Log Categories:**
- REQUEST - New request created
- ASSIGNMENT - Request assigned to elevator
- STATE - Elevator state transitions
- MOVEMENT - Elevator moving between floors
- EVENT - Significant events (arrival, boarding, etc.)
- ERROR - Errors and exceptions
- METRIC - Performance metrics

**Log Levels:**
- INFO - Normal operations
- DEBUG - Detailed debugging information
- WARN - Warnings (e.g., queue getting full)
- ERROR - Errors and exceptions

**Performance Metrics (Logged periodically):**
- Total requests processed
- Average wait time per request
- Average travel time
- Elevator utilization (% time moving vs idle)
- Current queue depth
- Requests completed vs pending

**Output Format:**
- Human-readable console text (primary)
- Structured format with timestamps
- Clear visual separation of status vs events
- Request IDs for traceability

---

### 5. Explicit Out of Scope

**From problem statement (see Phase 0):**
- Weight limits, Fire control, Overrides, Holds

**Additional exclusions for Phase 1:**
- Multi-building support
- Elevator maintenance scheduling
- Emergency protocols
- User authentication/authorization
- Request prioritization (VIP, emergency)
- Energy optimization
- Express elevators
- Advanced scheduling algorithms
- Graphical UI
- Historical analytics
- Elevator capacity limits
- Manual overrides
- Elevator malfunction handling

---

## Non-Functional Requirements

### 1. Latency Requirements

**Request Assignment Latency:**
- **Target:** < 1 second
- **Definition:** Time from request received to elevator assigned
- **Reasoning:** User expects quick acknowledgment

**Status Query Latency:**
- **Target:** < 1 second
- **Definition:** Time to return current status of all elevators
- **Reasoning:** Real-time monitoring needs fast response

**Elevator Fulfillment Latency:**
- **Variable:** Depends on elevator position and state
- **Calculation:** See Phase 2 Constraints for timing details

**Simulation Tick Accuracy:**
- **Configurable:** See Phase 2 Constraints for timing configuration

---

### 2. Throughput Requirements

**Normal Operation:**
- **Incoming request rate:** 1 request every 5 seconds = 12 requests/minute
- **Reasoning:** Typical office building usage pattern
- **Peak handling:** System should handle bursts of 5-10 requests/minute

**Maximum Concurrent Hall Calls:**
- **Queue capacity:** 18 unique hall calls (theoretical maximum)
- **Reasoning:** 18 unique (floor, direction) combinations possible
  - Floor 1: UP only (1)
  - Floor 10: DOWN only (1)
  - Floors 2-9: UP and DOWN (16)
- **Practical limit:** 15-20 hall calls in queue
- **Behavior when full:** Accept and merge with existing hall calls (idempotent)

**Elevator Throughput:**
- **Simultaneous operations:** All 4 elevators moving/serving simultaneously
- **Physical limit:** ~2.4 requests/minute (4 elevators, ~100s per request average)
- **Expected utilization:** 60-80% (elevators not always perfectly utilized)

**Stress Testing (Optional):**
- **Burst rate:** Up to 10 requests/second for short duration
- **Queue depth:** Up to 50 requests
- **Purpose:** Validate system handles extreme load gracefully


---

### 3. Consistency Requirements

**Strong Consistency (Required):**

**Hall Call Assignment:**
- Each hall call assigned to exactly one elevator
- No duplicate assignments
- Assignment is atomic operation
- Multiple destinations for same hall call served by assigned elevator

**State Updates:**
- Elevator position updates are atomic
- All reads see latest write immediately
- No stale state visible to queries

**Status Queries:**
- Status queries return current accurate state
- No eventual consistency delays
- Real-time visibility of all changes

**Passenger Tracking:**
- Destination updates are atomic
- Passenger boarding/exiting is atomic
- No passengers lost or duplicated

**Implementation details in Phase 2 Constraints (Concurrency section).**

**Consistency Guarantees:**
- **Read-after-write:** Any read after write sees the updated value
- **Linearizability:** All operations appear atomic and ordered
- **No split-brain:** Single source of truth for all state

---

### 4. Availability Requirements

**Uptime:**
- System can run continuously until explicitly stopped
- Not required to be 24/7 operational

**Fault Tolerance:**
- State loss on crash is acceptable (simulation system)
- System restarts from initial state

**Graceful Shutdown:**
- Required: Clean stop with resource cleanup

---

### 5. Ordering Guarantees

- FIFO for hall call queue
- No strict ordering across elevators

### 6. Durability Requirements

- In-memory state only (no persistence required)
- Configuration file persists across restarts

### 7. Fault Tolerance Requirements

- Log errors and continue operation  
- Graceful degradation under load

### 8. Security Requirements

- Not applicable (internal simulation)
- Input validation for correctness only

---

### 9. Observability Requirements

**Logging:**
- All elevator state changes and movements
- All hall calls (received, assigned, completed)
- Errors and exceptions
- Performance metrics (periodic)

**Log Levels:** INFO, DEBUG, WARN, ERROR

**Status Display:**
- Real-time status of all 4 elevators
- Pending hall calls
- Basic metrics (requests processed, queue depth)

---

### 10. Maintainability Requirements

**Code Quality:**
- **Clean code:** Readable, well-structured
- **Comments:** Explain complex logic, state transitions
- **Naming:** Clear, descriptive variable/method names
- **SOLID principles:** Single responsibility, open/closed, etc.

**Testing:**
- **Unit tests:** Core logic (scheduler, state machine, validation)
- **Integration tests:** Full simulation scenarios
- **Test coverage:** Adequate (aim for 70%+ on critical paths)
- **Test framework:** xUnit or NUnit (C#)

**Documentation:**
- **Code comments:** Explain why, not just what
- **README:** How to build, run, configure
- **Architecture docs:** High-level design (this document)
- **API documentation:** If/when REST API added

**Extensibility:**
- **Pluggable components:** Request generator, scheduler strategy
- **Interface-based design:** Allow future implementations
- **Configuration-driven:** Behavior controlled by config, not hardcoded

**Version Control:**
- **Git:** Standard version control
- **Commit messages:** Clear, descriptive
- **Branching:** Feature branches, main branch stable

---

### 11. Performance Requirements

**Resource Usage:**
- **Memory:** Minimal (in-memory state for 4 elevators + request queue)
- **CPU:** Low (simple algorithms, periodic updates)
- **Disk:** Configuration file only, optional logging

**Efficiency:** (Think it over for better performance?)
- **Algorithm complexity:** O(n) for elevator selection acceptable (n = 4)
- **No premature optimization:** Correctness > performance
- **Scalability:** Not required (fixed 4 elevators, single building)

**Response Time:**
- **UI responsiveness:** Console updates immediately
- **Query response:** < 100ms for status queries
- **State updates:** Immediate (synchronous)

---

## Summary of Key Requirements

| Category | Requirement | Priority |
|----------|-------------|----------|
| **Users** | Generator + Developers | Must have |
| **Requests** | Source + Destination (complete journey) | Must have |
| **Elevators** | 4 elevators, 10 floors | Must have |
| **Timing** | Configurable (10s default, 100ms test) | Must have |
| **Assignment** | < 1 sec latency | Must have |
| **Throughput** | 12 req/min normal, 20 pending max | Must have |
| **Consistency** | Strong consistency | Must have |
| **Logging** | Comprehensive console logging | Must have |
| **Testing** | Unit + integration tests | Must have |
| **Availability** | Continuous capable, not required | Nice to have |
| **Persistence** | Not required (in-memory only) | Out of scope |
| **UI** | Console only (no GUI) | Must have |
| **API** | Interface-based (REST future) | Nice to have |
