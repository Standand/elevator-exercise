namespace ElevatorSystem.Infrastructure.Configuration
{
    /// <summary>
    /// Configuration for the elevator simulation.
    /// </summary>
    public class SimulationConfiguration
    {
        public int MaxFloors { get; set; }
        public int ElevatorCount { get; set; }
        public int TickIntervalMs { get; set; }
        public int DoorOpenTicks { get; set; }
        public int ElevatorMovementTicks { get; set; }
        public int RequestIntervalSeconds { get; set; }

        /// <summary>
        /// Returns default configuration values.
        /// </summary>
        public static SimulationConfiguration Default()
        {
            return new SimulationConfiguration
            {
                MaxFloors = 10,
                ElevatorCount = 4,
                TickIntervalMs = 1000,
                DoorOpenTicks = 3,
                ElevatorMovementTicks = 3,
                RequestIntervalSeconds = 5
            };
        }
    }
}
