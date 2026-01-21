using System;
using ElevatorSystem.Domain.ValueObjects;

namespace ElevatorSystem.Domain.Entities
{
    /// <summary>
    /// Represents a passenger's request (journey from source to destination).
    /// Entity with identity (Id).
    /// </summary>
    public class Request
    {
        public Guid Id { get; }
        public Guid HallCallId { get; }
        public Journey Journey { get; }
        public RequestStatus Status { get; private set; }
        public DateTime CreatedAt { get; }

        public Request(Guid hallCallId, Journey journey)
        {
            Id = Guid.NewGuid();
            HallCallId = hallCallId;
            Journey = journey ?? throw new ArgumentNullException(nameof(journey));
            Status = RequestStatus.WAITING;
            CreatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Marks the request as in-transit (passenger boarded elevator).
        /// </summary>
        public void MarkAsInTransit()
        {
            if (Status != RequestStatus.WAITING)
                throw new InvalidOperationException($"Cannot mark request as in-transit from status {Status}");

            Status = RequestStatus.IN_TRANSIT;
        }

        /// <summary>
        /// Marks the request as completed (passenger reached destination).
        /// </summary>
        public void MarkAsCompleted()
        {
            if (Status != RequestStatus.IN_TRANSIT)
                throw new InvalidOperationException($"Cannot complete request from status {Status}");

            Status = RequestStatus.COMPLETED;
        }

        public override string ToString()
        {
            return $"Request {Id}: {Journey}, {Status}";
        }
    }
}
