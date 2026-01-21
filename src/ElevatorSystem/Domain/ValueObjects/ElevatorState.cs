using System;

namespace ElevatorSystem.Domain.ValueObjects
{
    /// <summary>
    /// Represents the state of an elevator (IDLE, MOVING, LOADING).
    /// Immutable value object with singleton instances.
    /// </summary>
    public class ElevatorState
    {
        public static readonly ElevatorState IDLE = new ElevatorState("IDLE");
        public static readonly ElevatorState MOVING = new ElevatorState("MOVING");
        public static readonly ElevatorState LOADING = new ElevatorState("LOADING");

        public string Value { get; }

        private ElevatorState(string value)
        {
            Value = value;
        }

        /// <summary>
        /// Factory method to create ElevatorState from string.
        /// </summary>
        public static ElevatorState Of(string value)
        {
            return value.ToUpper() switch
            {
                "IDLE" => IDLE,
                "MOVING" => MOVING,
                "LOADING" => LOADING,
                _ => throw new ArgumentException($"Invalid elevator state: {value}")
            };
        }

        public override bool Equals(object? obj)
        {
            return obj is ElevatorState other && Value == other.Value;
        }

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value;

        public static bool operator ==(ElevatorState? left, ElevatorState? right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left is null || right is null) return false;
            return left.Equals(right);
        }

        public static bool operator !=(ElevatorState? left, ElevatorState? right) => !(left == right);
    }
}
