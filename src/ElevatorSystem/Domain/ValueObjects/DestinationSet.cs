using System;
using System.Collections.Generic;
using System.Linq;

namespace ElevatorSystem.Domain.ValueObjects
{
    /// <summary>
    /// Manages the set of destination floors for an elevator.
    /// Encapsulates destination ordering logic based on direction.
    /// </summary>
    public class DestinationSet
    {
        private readonly SortedSet<int> _destinations = new SortedSet<int>();
        private Direction _direction;

        public DestinationSet(Direction direction)
        {
            _direction = direction;
        }

        public int Count => _destinations.Count;

        public bool IsEmpty => _destinations.Count == 0;

        /// <summary>
        /// Adds a destination floor.
        /// </summary>
        public void Add(int floor)
        {
            _destinations.Add(floor);
        }

        /// <summary>
        /// Removes a destination floor.
        /// </summary>
        public void Remove(int floor)
        {
            _destinations.Remove(floor);
        }

        /// <summary>
        /// Checks if a floor is in the destination set.
        /// </summary>
        public bool Contains(int floor)
        {
            return _destinations.Contains(floor);
        }

        /// <summary>
        /// Gets all destinations as a list.
        /// </summary>
        public List<int> GetAll()
        {
            return _destinations.ToList();
        }

        /// <summary>
        /// Gets the next destination based on current floor and direction.
        /// </summary>
        public int GetNextDestination(int currentFloor)
        {
            if (_destinations.Count == 0)
                throw new InvalidOperationException("No destinations available");

            if (_direction == Direction.UP)
            {
                // Return smallest destination >= currentFloor
                var candidates = _destinations.Where(d => d >= currentFloor).ToList();
                if (candidates.Any())
                    return candidates.Min();
                else
                    return _destinations.Max(); // Wrap around - furthest floor
            }
            else if (_direction == Direction.DOWN)
            {
                // Return largest destination <= currentFloor
                var candidates = _destinations.Where(d => d <= currentFloor).ToList();
                if (candidates.Any())
                    return candidates.Max();
                else
                    return _destinations.Min(); // Wrap around - lowest floor (including floor 0!)
            }
            else // IDLE
            {
                // Return nearest
                return _destinations.OrderBy(d => Math.Abs(d - currentFloor)).First();
            }
        }

        /// <summary>
        /// Gets the furthest destination in the current direction.
        /// </summary>
        public int GetFurthestDestination()
        {
            if (_destinations.Count == 0)
                throw new InvalidOperationException("No destinations available");

            if (_direction == Direction.UP)
                return _destinations.Max();
            else if (_direction == Direction.DOWN)
                return _destinations.Min();
            else // IDLE
                return _destinations.First();
        }

        /// <summary>
        /// Updates the direction (used when elevator changes direction).
        /// </summary>
        public void SetDirection(Direction direction)
        {
            _direction = direction;
        }
    }
}
