using System;
using System.Collections.Generic;

namespace ElevatorSystem.Domain.ValueObjects
{
    /// <summary>
    /// Represents a snapshot of the building's current status.
    /// Immutable value object for status queries.
    /// </summary>
    public record BuildingStatus
    {
        public required List<ElevatorStatus> Elevators { get; init; }
        public int PendingHallCallsCount { get; init; }
        public DateTime Timestamp { get; init; }

        public override string ToString()
        {
            return $"Building Status at {Timestamp:HH:mm:ss}: {Elevators.Count} elevators, {PendingHallCallsCount} pending hall calls";
        }
    }
}
