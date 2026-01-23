using System;
using System.Collections.Generic;
using System.Linq;
using ElevatorSystem.Infrastructure.Logging;

namespace ElevatorSystem.Tests.TestHelpers
{
    /// <summary>
    /// Mock logger for testing that captures all log messages.
    /// </summary>
    public class MockLogger : ILogger
    {
        public List<string> Messages { get; } = new List<string>();
        
        public void LogDebug(string message) => Messages.Add($"[DEBUG] {message}");
        public void LogInfo(string message) => Messages.Add($"[INFO] {message}");
        public void LogWarning(string message) => Messages.Add($"[WARN] {message}");
        public void LogError(string message) => Messages.Add($"[ERROR] {message}");
        
        public void Clear() => Messages.Clear();
        
        public bool Contains(string substring) => 
            Messages.Any(m => m.Contains(substring, StringComparison.OrdinalIgnoreCase));
        
        public int Count(string substring) =>
            Messages.Count(m => m.Contains(substring, StringComparison.OrdinalIgnoreCase));
    }
}
