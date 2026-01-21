using System;

namespace ElevatorSystem.Infrastructure.Time
{
    /// <summary>
    /// System clock implementation of ITimeService.
    /// </summary>
    public class SystemTimeService : ITimeService
    {
        public DateTime GetCurrentTime()
        {
            return DateTime.UtcNow;
        }
    }
}
