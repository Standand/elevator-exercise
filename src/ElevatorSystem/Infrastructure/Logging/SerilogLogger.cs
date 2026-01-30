using Serilog;
using Serilog.Events;

namespace ElevatorSystem.Infrastructure.Logging
{
    /// <summary>
    /// Serilog-based logger implementation with structured logging.
    /// Provides class context, log levels, timestamps, and structured messages.
    /// Note: This is an alternative to ConsoleLogger. ConsoleLogger now uses Serilog internally.
    /// </summary>
    public class SerilogLogger : ILogger
    {
        private readonly Serilog.ILogger _logger;

        public SerilogLogger(string className, LogEventLevel minimumLevel = LogEventLevel.Information)
        {
            _logger = new LoggerConfiguration()
                .MinimumLevel.Is(minimumLevel)
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss}] [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
                    theme: Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Code)
                .CreateLogger()
                .ForContext("SourceContext", className);
        }

        public void LogDebug(string message)
        {
            _logger.Debug(message);
        }

        public void LogInfo(string message)
        {
            _logger.Information(message);
        }

        public void LogWarning(string message)
        {
            _logger.Warning(message);
        }

        public void LogError(string message)
        {
            _logger.Error(message);
        }
    }

    /// <summary>
    /// Factory for creating Serilog loggers with class context.
    /// </summary>
    public static class LoggerFactory
    {
        public static ILogger CreateLogger<T>(LogEventLevel minimumLevel = LogEventLevel.Information)
        {
            return new SerilogLogger(typeof(T).Name, minimumLevel);
        }

        public static ILogger CreateLogger(string className, LogEventLevel minimumLevel = LogEventLevel.Information)
        {
            return new SerilogLogger(className, minimumLevel);
        }
    }
}
