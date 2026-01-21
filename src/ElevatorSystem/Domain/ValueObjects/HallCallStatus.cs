using System;

namespace ElevatorSystem.Domain.ValueObjects
{
    /// <summary>
    /// Represents the status of a hall call (PENDING, ASSIGNED, COMPLETED).
    /// Immutable value object with singleton instances.
    /// </summary>
    public class HallCallStatus
    {
        public static readonly HallCallStatus PENDING = new HallCallStatus("PENDING");
        public static readonly HallCallStatus ASSIGNED = new HallCallStatus("ASSIGNED");
        public static readonly HallCallStatus COMPLETED = new HallCallStatus("COMPLETED");

        public string Value { get; }

        private HallCallStatus(string value)
        {
            Value = value;
        }

        /// <summary>
        /// Factory method to create HallCallStatus from string.
        /// </summary>
        public static HallCallStatus Of(string value)
        {
            return value.ToUpper() switch
            {
                "PENDING" => PENDING,
                "ASSIGNED" => ASSIGNED,
                "COMPLETED" => COMPLETED,
                _ => throw new ArgumentException($"Invalid hall call status: {value}")
            };
        }

        public override bool Equals(object? obj)
        {
            return obj is HallCallStatus other && Value == other.Value;
        }

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value;

        public static bool operator ==(HallCallStatus? left, HallCallStatus? right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left is null || right is null) return false;
            return left.Equals(right);
        }

        public static bool operator !=(HallCallStatus? left, HallCallStatus? right) => !(left == right);
    }
}
