using System.Collections.Generic;
using ElevatorSystem.Domain.Entities;

namespace ElevatorSystem.Domain.Services
{
    /// <summary>
    /// Strategy interface for elevator selection algorithms.
    /// </summary>
    public interface ISchedulingStrategy
    {
        /// <summary>
        /// Selects the best elevator to service a hall call.
        /// </summary>
        /// <param name="hallCall">The hall call to service</param>
        /// <param name="elevators">Available elevators</param>
        /// <returns>Best elevator, or null if none available</returns>
        Elevator? SelectBestElevator(HallCall hallCall, List<Elevator> elevators);
    }
}
