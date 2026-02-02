using System;

namespace ElevatorSystem.Domain.ValueObjects
{
    /// <summary>
    /// Represents a passenger's journey from source to destination floor.
    /// Immutable value object.
    /// </summary>
    public record Journey
    {
        public int SourceFloor { get; init; }
        public int DestinationFloor { get; init; }
        public Direction Direction { get; init; }

        /// <summary>
        /// Factory method to create a Journey with validation.
        /// </summary>
        public static Journey Of(int sourceFloor, int destinationFloor)
        {
            if (sourceFloor == destinationFloor)
                throw new ArgumentException("Source and destination floors cannot be the same");

            if (sourceFloor < 0 || destinationFloor < 0)
                throw new ArgumentException("Floor numbers cannot be negative");

            var direction = destinationFloor > sourceFloor ? Direction.UP : Direction.DOWN;

            return new Journey
            {
                SourceFloor = sourceFloor,
                DestinationFloor = destinationFloor,
                Direction = direction
            };
        }

        public override string ToString() => $"{SourceFloor} → {DestinationFloor} ({Direction})";
    }
}
