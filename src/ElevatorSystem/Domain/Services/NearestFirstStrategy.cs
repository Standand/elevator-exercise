using System;
using System.Collections.Generic;
using System.Linq;
using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.ValueObjects;

namespace ElevatorSystem.Domain.Services
{
    /// <summary>
    /// Nearest-first scheduling strategy.
    /// Simply picks the nearest idle elevator, ignoring direction.
    /// </summary>
    public class NearestFirstStrategy : ISchedulingStrategy
    {
        public Elevator? SelectBestElevator(HallCall hallCall, List<Elevator> elevators)
        {
            // Pick nearest idle elevator
            return elevators
                .Where(e => e.State == ElevatorState.IDLE)
                .OrderBy(e => Math.Abs(e.CurrentFloor - hallCall.Floor))
                .FirstOrDefault();
        }
    }
}
