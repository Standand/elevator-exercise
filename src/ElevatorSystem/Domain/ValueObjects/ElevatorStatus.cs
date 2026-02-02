using System;
using System.Collections.Generic;

namespace ElevatorSystem.Domain.ValueObjects
{
    /// <summary>
    /// Represents a snapshot of an elevator's current status.
    /// Immutable value object for status queries.
    /// </summary>
    public record ElevatorStatus
    {
        public int Id { get; init; }
        public int CurrentFloor { get; init; }
        public Direction Direction { get; init; }
        public ElevatorState State { get; init; }
        public required List<int> Destinations { get; init; }
        public required List<Guid> AssignedHallCallIds { get; init; }

        public override string ToString()
        {
            var destStr = Destinations.Count > 0 ? string.Join(", ", Destinations) : "none";
            return $"Elevator {Id}: Floor {CurrentFloor}, {Direction}, {State}, Destinations: [{destStr}]";
        }
    }
}
