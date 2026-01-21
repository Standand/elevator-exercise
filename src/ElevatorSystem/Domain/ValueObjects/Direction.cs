using System;

namespace ElevatorSystem.Domain.ValueObjects
{
    /// <summary>
    /// Represents the direction of movement (UP, DOWN, IDLE).
    /// Immutable value object with singleton instances.
    /// </summary>
    public class Direction
    {
        public static readonly Direction UP = new Direction("UP");
        public static readonly Direction DOWN = new Direction("DOWN");
        public static readonly Direction IDLE = new Direction("IDLE");

        public string Value { get; }

        private Direction(string value)
        {
            Value = value;
        }

        /// <summary>
        /// Factory method to create Direction from string.
        /// </summary>
        public static Direction Of(string value)
        {
            return value.ToUpper() switch
            {
                "UP" => UP,
                "DOWN" => DOWN,
                "IDLE" => IDLE,
                _ => throw new ArgumentException($"Invalid direction: {value}")
            };
        }

        public override bool Equals(object? obj)
        {
            return obj is Direction other && Value == other.Value;
        }

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value;

        public static bool operator ==(Direction? left, Direction? right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left is null || right is null) return false;
            return left.Equals(right);
        }

        public static bool operator !=(Direction? left, Direction? right) => !(left == right);
    }
}
