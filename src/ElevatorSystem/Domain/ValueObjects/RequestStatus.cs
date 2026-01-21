using System;

namespace ElevatorSystem.Domain.ValueObjects
{
    /// <summary>
    /// Represents the status of a passenger request (WAITING, IN_TRANSIT, COMPLETED).
    /// Immutable value object with singleton instances.
    /// </summary>
    public class RequestStatus
    {
        public static readonly RequestStatus WAITING = new RequestStatus("WAITING");
        public static readonly RequestStatus IN_TRANSIT = new RequestStatus("IN_TRANSIT");
        public static readonly RequestStatus COMPLETED = new RequestStatus("COMPLETED");

        public string Value { get; }

        private RequestStatus(string value)
        {
            Value = value;
        }

        /// <summary>
        /// Factory method to create RequestStatus from string.
        /// </summary>
        public static RequestStatus Of(string value)
        {
            return value.ToUpper() switch
            {
                "WAITING" => WAITING,
                "IN_TRANSIT" => IN_TRANSIT,
                "COMPLETED" => COMPLETED,
                _ => throw new ArgumentException($"Invalid request status: {value}")
            };
        }

        public override bool Equals(object? obj)
        {
            return obj is RequestStatus other && Value == other.Value;
        }

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value;

        public static bool operator ==(RequestStatus? left, RequestStatus? right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left is null || right is null) return false;
            return left.Equals(right);
        }

        public static bool operator !=(RequestStatus? left, RequestStatus? right) => !(left == right);
    }
}
