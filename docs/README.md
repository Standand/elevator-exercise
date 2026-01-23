# Documentation Guide

## Primary Documents

### COMPLETE-DESIGN.md
Complete system design specification covering all 12 phases of the design process.

**Contents:**
- Problem statement and requirements analysis
- Domain model and entity relationships
- System architecture (Clean Architecture)
- Core algorithms (scheduling, tick processing, destination selection)
- Concurrency model and thread safety guarantees
- Error handling strategies
- Performance analysis and optimization
- Testing strategy and coverage targets

### FUTURE-IMPROVEMENTS.md
Planned enhancements and extensions beyond Phase 1 scope.

**Contents:**
- Feature roadmap (Phases 2-8)
- Technical debt documentation
- Migration strategies
- Priority matrix and success metrics

## Archive

The `archive/` folder contains original phase-by-phase design documents from the iterative design process. These are preserved for historical reference. All content has been consolidated into `COMPLETE-DESIGN.md`.

## Navigation Paths

### For Reviewers
1. Start with `COMPLETE-DESIGN.md` (complete specification)
2. Review `../README.md` (project overview with ADRs)
3. Explore `../src/` (implementation code)
4. Run simulation: `cd ../src/ElevatorSystem && dotnet run`

### For Developers
1. Read `COMPLETE-DESIGN.md` (design specifications)
2. Consult `../src/README.md` (developer guide and API reference)
3. Review `FUTURE-IMPROVEMENTS.md` (extension points)
4. Begin implementation

### For Testers
1. Review Section 11 in `COMPLETE-DESIGN.md` (testing strategy)
2. Refer to `../src/README.md` for test execution commands
3. Target 90% code coverage (70% unit, 20% integration, 10% E2E)
4. Use xUnit framework with Moq for test doubles

## Document Status

All 12 design phases complete and consolidated. Implementation finished. Testing strategy defined, implementation in progress.
