using System;
using System.Collections.Generic;
using System.Linq;
using ElevatorSystem.Domain.ValueObjects;

namespace ElevatorSystem.Domain.Entities
{
    /// <summary>
    /// Represents a hall call (button press at a floor requesting UP or DOWN).
    /// Entity with identity (Id).
    /// Multiple passengers (Requests) can share the same HallCall.
    /// </summary>
    public class HallCall
    {
        public Guid Id { get; }
        public int Floor { get; }
        public Direction Direction { get; }
        public HallCallStatus Status { get; private set; }
        public int? AssignedElevatorId { get; private set; }
        public DateTime CreatedAt { get; }
        
        private readonly HashSet<int> _destinationFloors = new HashSet<int>();

        public HallCall(int floor, Direction direction)
        {
            Id = Guid.NewGuid();
            Floor = floor;
            Direction = direction ?? throw new ArgumentNullException(nameof(direction));
            Status = HallCallStatus.PENDING;
            AssignedElevatorId = null;
            CreatedAt = DateTime.UtcNow;
        }
        
        /// <summary>
        /// Adds a destination floor for a passenger using this hall call.
        /// </summary>
        public void AddDestination(int destinationFloor)
        {
            _destinationFloors.Add(destinationFloor);
        }
        
        /// <summary>
        /// Gets all destination floors for this hall call.
        /// </summary>
        public IReadOnlyCollection<int> GetDestinations()
        {
            return _destinationFloors.ToList().AsReadOnly();
        }

        /// <summary>
        /// Marks the hall call as assigned to an elevator.
        /// </summary>
        public void MarkAsAssigned(int elevatorId)
        {
            if (Status != HallCallStatus.PENDING)
                throw new InvalidOperationException($"Cannot assign hall call in status {Status}");

            AssignedElevatorId = elevatorId;
            Status = HallCallStatus.ASSIGNED;
        }

        /// <summary>
        /// Marks the hall call as completed.
        /// </summary>
        public void MarkAsCompleted()
        {
            if (Status != HallCallStatus.ASSIGNED)
                throw new InvalidOperationException($"Cannot complete hall call in status {Status}");

            Status = HallCallStatus.COMPLETED;
        }

        public override string ToString()
        {
            return $"HallCall {Id}: Floor {Floor}, {Direction}, {Status}";
        }
    }
}
