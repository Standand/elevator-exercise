using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.ValueObjects;

namespace ElevatorSystem.Tests.Integration.TestHelpers
{
    /// <summary>
    /// Helper for simulating multiple ticks in integration tests.
    /// </summary>
    public static class TickSimulator
    {
        /// <summary>
        /// Processes the specified number of ticks.
        /// </summary>
        public static void ProcessTicks(Building building, int tickCount)
        {
            for (int i = 0; i < tickCount; i++)
            {
                building.ProcessTick();
            }
        }

        /// <summary>
        /// Processes ticks until the specified condition is met or max ticks reached.
        /// </summary>
        public static bool ProcessTicksUntil(Building building, Func<BuildingStatus, bool> condition, int maxTicks = 100)
        {
            for (int i = 0; i < maxTicks; i++)
            {
                building.ProcessTick();
                var status = building.GetStatus();
                if (condition(status))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Processes ticks until elevator reaches target floor.
        /// </summary>
        public static bool ProcessTicksUntilElevatorReachesFloor(Building building, int elevatorId, int targetFloor, int maxTicks = 100)
        {
            return ProcessTicksUntil(building, status =>
            {
                var elevator = status.Elevators.FirstOrDefault(e => e.Id == elevatorId);
                return elevator != null && elevator.CurrentFloor == targetFloor && elevator.State == ElevatorState.LOADING;
            }, maxTicks);
        }
    }
}
