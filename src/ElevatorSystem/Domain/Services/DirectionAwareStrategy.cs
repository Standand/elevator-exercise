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
            var candidateElevators = GetCandidateElevators(hallCall, elevators);

            if (!candidateElevators.Any())
            {
                return null;
            }

            var elevatorsInSameDirection = GetElevatorsInSameDirection(hallCall, candidateElevators);

            if (elevatorsInSameDirection.Any())
            {
                return GetNearestElevator(hallCall, elevatorsInSameDirection);
            }

            return GetNearestElevator(hallCall, candidateElevators);
        }

        private static List<Elevator> GetCandidateElevators(HallCall hallCall, List<Elevator> elevators)
        {
            return elevators
                .Where(e => e.CanAcceptHallCall(hallCall))
                .ToList();
        }

        private static List<Elevator> GetElevatorsInSameDirection(HallCall hallCall, List<Elevator> candidates)
        {
            return candidates
                .Where(e => e.Direction == hallCall.Direction)
                .ToList();
        }

        private static Elevator GetNearestElevator(HallCall hallCall, List<Elevator> elevators)
        {
            return elevators
                .OrderBy(e => Math.Abs(e.CurrentFloor - hallCall.Floor))
                .First();
        }
    }
}
