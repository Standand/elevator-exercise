using System;

namespace ElevatorSystem.Infrastructure.Time
{
    /// <summary>
    /// Interface for time-related operations (enables testing).
    /// </summary>
    public interface ITimeService
    {
        DateTime GetCurrentTime();
    }
}
