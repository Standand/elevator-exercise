using System;
using System.Collections.Generic;

namespace ElevatorSystem.Domain.ValueObjects
{
    /// <summary>
    /// Represents a snapshot of the building's current status.
    /// Immutable value object for status queries.
    /// </summary>
    public class BuildingStatus
    {
        public List<ElevatorStatus> Elevators { get; }
        public int PendingHallCallsCount { get; }
        public DateTime Timestamp { get; }

        public BuildingStatus(
            List<ElevatorStatus> elevators,
            int pendingHallCallsCount,
            DateTime timestamp)
        {
            Elevators = elevators ?? throw new ArgumentNullException(nameof(elevators));
            PendingHallCallsCount = pendingHallCallsCount;
            Timestamp = timestamp;
        }

        public override string ToString()
        {
            return $"Building Status at {Timestamp:HH:mm:ss}: {Elevators.Count} elevators, {PendingHallCallsCount} pending hall calls";
        }
    }
}
