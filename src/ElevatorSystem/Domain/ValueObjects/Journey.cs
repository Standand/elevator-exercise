using System;

namespace ElevatorSystem.Domain.ValueObjects
{
    /// <summary>
    /// Represents a passenger's journey from source to destination floor.
    /// Immutable value object.
    /// </summary>
    public class Journey
    {
        public int SourceFloor { get; }
        public int DestinationFloor { get; }
        public Direction Direction { get; }

        private Journey(int sourceFloor, int destinationFloor)
        {
            SourceFloor = sourceFloor;
            DestinationFloor = destinationFloor;
            Direction = destinationFloor > sourceFloor ? ValueObjects.Direction.UP : ValueObjects.Direction.DOWN;
        }

        /// <summary>
        /// Factory method to create a Journey with validation.
        /// </summary>
        public static Journey Of(int sourceFloor, int destinationFloor)
        {
            if (sourceFloor == destinationFloor)
                throw new ArgumentException("Source and destination floors cannot be the same");

            if (sourceFloor < 0 || destinationFloor < 0)
                throw new ArgumentException("Floor numbers cannot be negative");

            return new Journey(sourceFloor, destinationFloor);
        }

        public override bool Equals(object? obj)
        {
            return obj is Journey other &&
                   SourceFloor == other.SourceFloor &&
                   DestinationFloor == other.DestinationFloor;
        }

        public override int GetHashCode() => HashCode.Combine(SourceFloor, DestinationFloor);

        public override string ToString() => $"{SourceFloor} → {DestinationFloor} ({Direction})";
    }
}
