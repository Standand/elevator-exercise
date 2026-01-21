using System;
using System.Collections.Generic;

namespace ElevatorSystem.Domain.ValueObjects
{
    /// <summary>
    /// Represents a snapshot of an elevator's current status.
    /// Immutable value object for status queries.
    /// </summary>
    public class ElevatorStatus
    {
        public int Id { get; }
        public int CurrentFloor { get; }
        public Direction Direction { get; }
        public ElevatorState State { get; }
        public List<int> Destinations { get; }
        public List<Guid> AssignedHallCallIds { get; }

        public ElevatorStatus(
            int id,
            int currentFloor,
            Direction direction,
            ElevatorState state,
            List<int> destinations,
            List<Guid> assignedHallCallIds)
        {
            Id = id;
            CurrentFloor = currentFloor;
            Direction = direction ?? throw new ArgumentNullException(nameof(direction));
            State = state ?? throw new ArgumentNullException(nameof(state));
            Destinations = destinations ?? throw new ArgumentNullException(nameof(destinations));
            AssignedHallCallIds = assignedHallCallIds ?? throw new ArgumentNullException(nameof(assignedHallCallIds));
        }

        public override string ToString()
        {
            var destStr = Destinations.Count > 0 ? string.Join(", ", Destinations) : "none";
            return $"Elevator {Id}: Floor {CurrentFloor}, {Direction}, {State}, Destinations: [{destStr}]";
        }
    }
}
