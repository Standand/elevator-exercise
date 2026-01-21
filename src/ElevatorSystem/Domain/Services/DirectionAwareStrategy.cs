using System;
using System.Collections.Generic;
using System.Linq;
using ElevatorSystem.Domain.Entities;

namespace ElevatorSystem.Domain.Services
{
    /// <summary>
    /// Direction-aware scheduling strategy.
    /// Prioritizes elevators already moving in the same direction.
    /// </summary>
    public class DirectionAwareStrategy : ISchedulingStrategy
    {
        public Elevator? SelectBestElevator(HallCall hallCall, List<Elevator> elevators)
        {
            // 1. Filter elevators that can accept this hall call
            var candidates = elevators
                .Where(e => e.CanAcceptHallCall(hallCall))
                .ToList();

            if (!candidates.Any())
                return null;

            // 2. Prioritize elevators already moving in same direction
            var sameDirection = candidates
                .Where(e => e.Direction == hallCall.Direction)
                .ToList();

            if (sameDirection.Any())
            {
                // Pick nearest elevator moving in same direction
                return sameDirection
                    .OrderBy(e => Math.Abs(e.CurrentFloor - hallCall.Floor))
                    .First();
            }

            // 3. Otherwise, pick nearest idle elevator
            return candidates
                .OrderBy(e => Math.Abs(e.CurrentFloor - hallCall.Floor))
                .First();
        }
    }
}
