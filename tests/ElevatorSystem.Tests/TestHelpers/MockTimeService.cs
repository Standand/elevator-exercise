using System;
using ElevatorSystem.Infrastructure.Time;

namespace ElevatorSystem.Tests.TestHelpers
{
    /// <summary>
    /// Mock time service for testing that allows time manipulation.
    /// Enables instant tests without Thread.Sleep().
    /// </summary>
    public class MockTimeService : ITimeService
    {
        private DateTime _currentTime = DateTime.UtcNow;
        
        public DateTime GetCurrentTime() => _currentTime;
        
        /// <summary>
        /// Advances time by the specified duration.
        /// </summary>
        public void AdvanceTime(TimeSpan duration)
        {
            _currentTime = _currentTime.Add(duration);
        }
        
        /// <summary>
        /// Sets the current time to a specific value.
        /// </summary>
        public void SetTime(DateTime time)
        {
            _currentTime = time;
        }
        
        /// <summary>
        /// Resets time to DateTime.UtcNow.
        /// </summary>
        public void Reset()
        {
            _currentTime = DateTime.UtcNow;
        }
    }
}
