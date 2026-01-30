# Elevator Scheduling Algorithms

## Current Implementation: Direction-Aware Strategy

The system currently uses a **Direction-Aware Strategy** that:

1. **Prioritizes elevators moving in the same direction** as the hall call
2. **Falls back to IDLE elevators** if no moving elevators are available
3. **Uses cost-based selection** considering:
   - Distance to hall call floor
   - Whether elevator needs to extend its route
   - Tie-breaking by elevator ID

### Cost Calculation

```csharp
Cost = Distance + Route Extension Penalty - IDLE Bonus
```

- **Distance**: Absolute difference between elevator floor and hall call floor
- **Route Extension Penalty**: +100 if elevator must go past its furthest destination
- **IDLE Bonus**: -1 (slight preference for IDLE elevators)

### Advantages

- ✅ Simple and efficient
- ✅ Good for most scenarios
- ✅ Direction-aware (reduces unnecessary direction changes)
- ✅ Handles multiple elevators well

### Limitations

- ❌ Doesn't optimize for picking up multiple requests along the way
- ❌ May not minimize total wait time in high-traffic scenarios
- ❌ Doesn't consider future requests

---

## Alternative Algorithm: SCAN (Elevator Algorithm)

### Overview

SCAN is a disk scheduling algorithm adapted for elevators. It's also known as the "Elevator Algorithm" because it mimics how elevators work:

1. **Elevator moves in one direction** (UP or DOWN)
2. **Services all requests** in that direction
3. **Reverses direction** only when no more requests exist in current direction
4. **Repeats** the process

### How SCAN Works

```
Example: Elevator at floor 3, requests at floors [1, 5, 7, 2]

1. Elevator moves UP (no requests below current floor in UP direction)
2. Services floor 5 (first request encountered going UP)
3. Services floor 7 (next request going UP)
4. Reverses to DOWN (no more requests going UP)
5. Services floor 2 (first request going DOWN)
6. Services floor 1 (next request going DOWN)
7. Reverses to UP (no more requests going DOWN)
```

### Implementation Strategy

For a multi-elevator system, SCAN can be implemented as:

1. **Each elevator maintains a direction** (UP/DOWN/IDLE)
2. **When a hall call arrives:**
   - If elevator is IDLE: Assign if it's the nearest
   - If elevator is moving: Assign only if hall call is "on the way" in current direction
3. **Elevator services all stops** in current direction before reversing
4. **Direction reversal** happens when no more destinations exist in current direction

### Advantages

- ✅ **Optimal for throughput**: Services maximum requests per trip
- ✅ **Predictable**: Elevators follow a clear pattern
- ✅ **Efficient**: Minimizes direction changes
- ✅ **Good for high-traffic**: Handles many requests efficiently

### Disadvantages

- ❌ **Starvation risk**: Requests in opposite direction may wait long
- ❌ **Not optimal for low traffic**: May be overkill for simple scenarios
- ❌ **Complex coordination**: Harder to coordinate multiple elevators
- ❌ **Poor for scattered requests**: If requests are spread out, may be inefficient

### When to Use SCAN

- ✅ High-traffic scenarios (many requests)
- ✅ Requests tend to cluster by direction
- ✅ Throughput is more important than individual wait times
- ✅ Predictable patterns (e.g., rush hour up, end of day down)

### When NOT to Use SCAN

- ❌ Low-traffic scenarios (current Direction-Aware is better)
- ❌ Requests are scattered randomly
- ❌ Individual wait time is critical
- ❌ Need to minimize maximum wait time

---

## Other Algorithms to Consider

### 1. LOOK Algorithm

Similar to SCAN but **reverses direction immediately** when no more requests exist in current direction (doesn't go to end of building).

**Advantage**: More responsive than SCAN
**Disadvantage**: Slightly more complex

### 2. C-SCAN (Circular SCAN)

Always moves in one direction, services all requests, then **jumps to the other end** and continues.

**Advantage**: Fairer (no starvation)
**Disadvantage**: May be inefficient (jumps to end even if no requests there)

### 3. SSTF (Shortest Seek Time First)

Always services the **nearest request** regardless of direction.

**Advantage**: Minimizes individual wait times
**Disadvantage**: Can cause starvation, frequent direction changes

### 4. FCFS (First Come First Served)

Services requests in **order received**.

**Advantage**: Simple, fair
**Disadvantage**: Very inefficient, not used in practice

---

## Recommendation

**Current Direction-Aware Strategy is appropriate for:**
- General-purpose elevator systems
- Moderate traffic
- Need for balance between efficiency and responsiveness

**Consider SCAN if:**
- Traffic is very high
- Requests cluster by direction (rush hours)
- Throughput is the primary concern
- You can accept longer wait times for some requests

**Hybrid Approach:**
- Use Direction-Aware for normal operation
- Switch to SCAN during high-traffic periods
- Or use SCAN for specific elevators (e.g., express elevators)

---

## Implementation Notes

If implementing SCAN:

1. **Modify `DirectionAwareStrategy`** or create `ScanStrategy`
2. **Update `CanAcceptHallCall`** logic to only accept requests "on the way"
3. **Ensure direction reversal** happens only when no destinations exist in current direction
4. **Add metrics** to compare performance vs Direction-Aware
5. **Consider hybrid** approach based on traffic patterns

The current `DirectionAwareStrategy` already has some SCAN-like behavior:
- Elevators maintain direction
- Only accept requests in same direction
- Service all stops before reversing

The main difference is that SCAN is more strict about not accepting requests in opposite direction, even if the elevator is closer.
