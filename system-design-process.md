# System Design Process - 12 Phases

## PHASE 0 — READ THE PROBLEM PROPERLY (Most people already fail)

**Your job:**
Understand what is being asked, not what you wish was being asked.

**Ask yourself:**
- Is this:
  - Backend system?
  - Distributed system?
  - Real-time?
  - Stateful or stateless?
  - Is correctness more important than latency?
  - Is this internal system or public API?
- What are the success criteria?
- What are the business goals?
- Is this a greenfield project or extending an existing system?
- What are the key metrics that define success?
- What are the explicit constraints mentioned?
- What assumptions can I safely make?
- What are the edge cases mentioned or implied?
- Is this a batch processing or streaming system?
- Is this event-driven or request-response?
- What is the expected lifetime of this system?

🚫 **Do NOT think about databases, queues, or frameworks yet.**

---

## PHASE 1 — REQUIREMENTS (Functional + Non-Functional)

### 1. Functional Requirements (WHAT)
What must the system do?

**Ask:**
- Who are the users?
- What actions can they perform?
- What inputs?
- What outputs?
- What is explicitly out of scope?
- What are the edge cases?
- What are the error scenarios?
- What are the business rules?
- What are the validation rules?
- What are the acceptance criteria?
- What are the success scenarios?
- What happens in concurrent scenarios?
- What are the state transitions?
- What are the workflows?

**Example:**
"Users can request an elevator by pressing up/down"

### 2. Non-Functional Requirements (HOW WELL)
This is where seniors separate themselves.

**Ask:**
- Latency (ms? seconds?) - P50, P95, P99?
- Throughput (req/sec?) - peak vs average?
- Consistency vs availability? (CAP theorem)
- Ordering guarantees? (strict, causal, eventual)
- Durability? (how long must data persist?)
- Fault tolerance? (what failures must we handle?)
- Security? (authentication, authorization, encryption)
- Observability? (logging, metrics, tracing)
- Maintainability? (code quality, documentation)
- Extensibility? (how easy to add features?)
- Compliance? (GDPR, HIPAA, SOC2, etc.)
- Cost constraints? (budget limits?)

**Rule:**
If you don't state NFRs, reviewers assume you ignored them.

---

## PHASE 2 — CONSTRAINTS (Reality Check)

Constraints kill bad designs early.

**Ask:**
- Max users? (concurrent vs total)
- Data size? (storage, memory, network)
- Read/write ratio? (80/20, 90/10, etc.)
- Real-time constraints? (hard vs soft deadlines)
- Hardware limits? (CPU, memory, disk, network)
- Cloud / on-prem? (deployment model)
- Network constraints? (bandwidth, latency, geographic)
- Geographic constraints? (multi-region, data residency)
- Regulatory/compliance constraints? (data location, retention)
- Budget constraints? (cost per request, infrastructure costs)
- Team size/skills constraints? (what can we build/maintain?)
- Time to market constraints? (MVP vs full solution)
- Technology constraints? (must use existing stack?)
- Third-party dependencies? (external services, APIs)

**Say things like:**
"Assume 10k concurrent users, P99 latency < 200ms"

You are allowed to assume, but you must say them.

---

## PHASE 3 — DOMAIN MODEL (Most people skip this — don't)

**Your goal:**
Identify core concepts, not tables or classes.

**Ask:**
- What are the nouns?
- What has identity?
- What changes over time?
- What are the relationships between entities?
- What are the invariants? (rules that must always be true)
- What are the value objects vs entities?
- What are the aggregates? (consistency boundaries)
- What are the domain events? (things that happened)
- What are the commands? (things to do)
- What are the queries? (things to read)
- What are the bounded contexts? (different views of domain)
- What are the lifecycle states? (created, active, completed, etc.)

**Example (Elevator):**
- Elevator (entity)
- Request (entity)
- Floor (value object)
- Controller (service/aggregate root)
- Direction (enum/value object)
- ElevatorState (state machine)

This prevents spaghetti designs later.

---

## PHASE 4 — RESPONSIBILITIES (SRP at system level)

For each entity, ask:
- What does it own?
- What does it NOT own?
- What are the boundaries? (what can it access?)
- What are the interfaces? (how do others interact with it?)
- What are the dependencies? (what does it need?)
- What are the side effects? (what changes outside itself?)
- What is the single reason to change?
- What are the cohesion boundaries? (what belongs together?)
- What are the coupling points? (where does it connect?)
- Who is responsible for validation?
- Who is responsible for persistence?
- Who is responsible for orchestration?

**Example:**
- Controller → assignment logic (owns: request-to-elevator mapping)
- Elevator → movement execution (owns: current state, position)
- UI → input only (owns: user interaction, displays state)
- Request → request data (owns: source, destination, direction)

🚨 **Never mix responsibilities**
If something "knows too much", your design will break.

---

## PHASE 5 — HIGH LEVEL DESIGN (HLD)

Now you draw boxes.

**Ask:**
- What are the major components?
- Who talks to whom?
- Sync or async?
- Stateless or stateful?
- What are the deployment models? (monolith, microservices, serverless)
- What are the communication patterns? (request-response, pub-sub, event-driven)
- What are the data flow patterns? (fan-out, fan-in, pipeline)
- What are the security boundaries? (trust zones, authentication layers)
- What are the network topologies? (mesh, star, hub-and-spoke)
- What are the load balancing strategies?
- What are the service discovery mechanisms?
- What are the configuration management approaches?
- What are the deployment strategies? (blue-green, canary, rolling)

**Typical building blocks:**
- API Gateway
- Services
- DB
- Cache
- Queue
- Scheduler
- Message Broker
- Service Mesh
- Load Balancer
- CDN

⚠️ **You don't add tech because it's cool.**
You add tech because a requirement forced you to.

If you can't justify Redis → remove it.

---

## PHASE 6 — DATA DESIGN (This is where systems live or die)

**Ask:**
- What data is stored?
- What is mutable vs immutable?
- What needs transactions?
- What needs ordering?
- What is the source of truth?
- What are the access patterns? (read-heavy, write-heavy, mixed)
- What are the query patterns? (point lookups, range scans, aggregations)
- What are the update patterns? (append-only, in-place updates, versioned)
- What are the consistency requirements? (strong, eventual, causal)
- What are the retention policies? (TTL, archival, deletion)
- What are the data relationships? (one-to-one, one-to-many, many-to-many)
- What are the indexing requirements?
- What are the partitioning strategies?
- What are the replication strategies?
- What are the backup and recovery requirements?
- What is the data lifecycle? (create, read, update, delete, archive)
- What are the data migration needs?

**Think in:**
- Aggregates (consistency boundaries)
- Ownership (who owns what data)
- Write paths vs read paths
- Event sourcing vs CRUD
- CQRS (Command Query Responsibility Segregation)

🚫 **Tables ≠ design**
Design the data flow first, schema later.

---

## PHASE 7 — APIs & CONTRACTS

**Ask:**
- Who calls whom?
- What is the request/response?
- Idempotency? (can we safely retry?)
- Versioning? (how do we evolve APIs?)
- What are the error responses? (4xx, 5xx codes)
- What are the rate limits? (throttling strategy)
- What are the authentication/authorization requirements?
- What are the pagination requirements? (cursor vs offset)
- What are the filtering/sorting capabilities?
- What are the batch operations? (bulk requests)
- What are the webhook/notification mechanisms?
- What are the async operation patterns? (polling, callbacks)
- What are the request/response sizes? (payload limits)
- What are the timeout requirements?
- What are the retry policies?
- What are the circuit breaker thresholds?
- What are the API documentation requirements? (OpenAPI, GraphQL schema)

**Example:**
```
POST /requests
{
  "sourceFloor": 5,
  "direction": "UP"
}
```

Contracts are promises.
Breaking them is how systems fail silently.

---

## PHASE 8 — FAILURE MODES (THIS IS CRITICAL)

Most candidates ignore this. Reviewers love it.

**Ask:**
- What if this service is down?
- What if messages are duplicated?
- What if requests arrive out of order?
- What if data is partially written?
- What are the partial failures? (some components work, others don't)
- What are the cascading failures? (one failure causes others)
- What are the data corruption scenarios?
- What are the network partition scenarios? (split-brain)
- What are the slow responses? (timeouts, degradation)
- What are the resource exhaustion scenarios? (memory, CPU, disk)
- What are the dependency failures? (external services down)
- What are the configuration errors?
- What are the deployment failures? (bad releases)
- What are the data loss scenarios?
- What are the security breach scenarios?
- What are the denial of service scenarios?

**Say things like:**
- Retry with backoff (exponential, jitter)
- Dead-letter queue (failed message handling)
- Idempotent consumers (safe to process twice)
- Circuit breakers (fail fast when dependency is down)
- Bulkheads (isolate failures)
- Timeouts and deadlines
- Graceful degradation (reduce functionality, not fail completely)
- Health checks and readiness probes
- Rate limiting and throttling
- Request deduplication
- Saga pattern (distributed transactions)
- Outbox pattern (reliable messaging)

This is where you stop sounding junior.

---

## PHASE 9 — SCALABILITY & PERFORMANCE

Only now do you scale.

**Ask:**
- What is the bottleneck? (CPU, memory, I/O, network, database)
- Horizontal or vertical scaling?
- Partitioning strategy? (by user, by region, by time)
- Caching strategy? (what to cache, where, TTL)
- Sharding key? (how to distribute data)
- What are the hot spots? (uneven load distribution)
- What are the cold start problems? (serverless, containers)
- What are the database scaling strategies? (read replicas, sharding, partitioning)
- What are the cache invalidation strategies?
- What are the load balancing algorithms? (round-robin, least connections, consistent hashing)
- What are the connection pooling strategies?
- What are the batch processing strategies? (reduce round trips)
- What are the compression strategies? (data, network)
- What are the CDN requirements? (static content, edge caching)
- What are the database query optimization needs? (indexes, query patterns)
- What are the async processing opportunities? (offload heavy work)

**Never say "we can scale later" — that's a red flag.**

---

## PHASE 10 — LOW LEVEL DESIGN (LLD)

Now — and only now — you write classes.

**Ask:**
- What interfaces exist?
- What dependencies?
- What patterns apply?
- What are the class responsibilities? (SRP)
- What are the abstractions? (interfaces, abstract classes)
- What are the concrete implementations?
- What are the dependency injection points?
- What are the factory methods/classes?
- What are the builder patterns?
- What are the state machines?
- What are the data structures? (collections, custom types)
- What are the algorithms? (sorting, searching, scheduling)
- What are the concurrency patterns? (locks, atomic operations, async/await)
- What are the error handling strategies? (exceptions, result types)
- What are the validation rules? (input validation, business rules)
- What are the logging and monitoring hooks?

**Examples:**
- Strategy (scheduling algorithms)
- Observer (events)
- Factory (object creation)
- Repository (data access abstraction)
- Service (business logic)
- Builder (complex object construction)
- Singleton (shared resources - use carefully)
- Command (encapsulate requests)
- State (state machines)
- Template Method (algorithm skeleton)

LLD should feel obvious if HLD was good.

---

## PHASE 11 — CODE (Thin, Boring, Predictable)

Good system design leads to boring code.

**Ask:**
- Where does logic live?
- What is pure? (no side effects, testable)
- What is stateful? (needs careful management)
- What is injected? (dependencies, configuration)
- What are the naming conventions?
- What are the code organization principles? (packages, modules)
- What are the error handling patterns? (try-catch, result types, optionals)
- What are the logging practices? (structured logging, log levels)
- What are the code review considerations?
- What are the documentation requirements? (comments, README, API docs)
- What are the code style guidelines?
- What are the performance considerations? (avoid premature optimization)
- What are the security considerations? (input sanitization, SQL injection, XSS)
- What are the concurrency safety measures? (thread-safety, race conditions)

If code feels complex → design was wrong.

---

## PHASE 12 — TESTING STRATEGY

**Ask:**
- What can be unit tested? (pure functions, isolated logic)
- What needs integration tests? (component interactions)
- What needs chaos testing? (failure scenarios)
- What needs end-to-end tests? (full user workflows)
- What needs performance tests? (load, stress, spike)
- What needs security tests? (penetration, vulnerability scanning)
- What needs contract tests? (API compatibility)
- What needs idempotency tests? (safe retries)
- What needs failure injection? (chaos engineering)
- What are the test data strategies? (fixtures, factories, mocks)
- What are the test isolation requirements? (parallel execution)
- What are the test coverage targets? (line, branch, path coverage)
- What are the regression test strategies?
- What are the smoke test strategies? (quick validation)
- What are the canary test strategies? (validate in production)
- What are the A/B test requirements?
- What are the monitoring and alerting needs? (production observability)

**Mention:**
- Contract tests (API compatibility between services)
- Idempotency tests (verify safe retries)
- Failure injection (chaos engineering tools)
- Property-based tests (generate test cases)
- Mutation testing (verify test quality)
- Performance benchmarks (establish baselines)
- Load testing (simulate production traffic)
- Security testing (OWASP, penetration testing)
