using System;

namespace ElevatorSystem.Infrastructure.Logging
{
    /// <summary>
    /// Console-based logger implementation.
    /// </summary>
    public class ConsoleLogger : ILogger
    {
        private readonly bool _enableDebug;

        public ConsoleLogger(bool enableDebug = false)
        {
            _enableDebug = enableDebug;
        }

        public void LogDebug(string message)
        {
            if (_enableDebug)
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DEBUG] {message}");
                Console.ResetColor();
            }
        }

        public void LogInfo(string message)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [INFO] {message}");
            Console.ResetColor();
        }

        public void LogWarning(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [WARN] {message}");
            Console.ResetColor();
        }

        public void LogError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [ERROR] {message}");
            Console.ResetColor();
        }
    }
}
