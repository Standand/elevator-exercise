using System;
using Serilog;
using Serilog.Events;

namespace ElevatorSystem.Infrastructure.Logging
{
    /// <summary>
    /// Console-based logger implementation using Serilog for structured logging.
    /// Provides class context, log levels, timestamps, and structured messages.
    /// </summary>
    public class ConsoleLogger : ILogger
    {
        private readonly Serilog.ILogger _serilogLogger;
        private readonly bool _enableDebug;

        public ConsoleLogger(bool enableDebug = false, string className = "System")
        {
            _enableDebug = enableDebug;
            var minLevel = enableDebug ? LogEventLevel.Debug : LogEventLevel.Information;
            
            _serilogLogger = new LoggerConfiguration()
                .MinimumLevel.Is(minLevel)
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss}] [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
                    theme: Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Code)
                .CreateLogger()
                .ForContext("SourceContext", className);
        }

        public void LogDebug(string message)
        {
            if (_enableDebug)
            {
                _serilogLogger.Debug(message);
            }
        }

        public void LogInfo(string message)
        {
            _serilogLogger.Information(message);
        }

        public void LogWarning(string message)
        {
            _serilogLogger.Warning(message);
        }

        public void LogError(string message)
        {
            _serilogLogger.Error(message);
        }
    }
}
