using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ElevatorSystem.Infrastructure.Configuration
{
    /// <summary>
    /// Loads and validates simulation configuration from JSON file.
    /// </summary>
    public static class ConfigurationLoader
    {
        public static SimulationConfiguration Load(string path = "appsettings.json")
        {
            // File missing - use defaults
            if (!File.Exists(path))
            {
                Console.WriteLine($"WARNING: Configuration file '{path}' not found. Using default values.");
                return SimulationConfiguration.Default();
            }

            try
            {
                // Parse JSON
                var json = File.ReadAllText(path);
                var config = JsonSerializer.Deserialize<SimulationConfiguration>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (config == null)
                {
                    Console.WriteLine($"WARNING: Failed to parse '{path}'. Using default values.");
                    return SimulationConfiguration.Default();
                }

                // Validate (throws on invalid)
                Validate(config);

                return config;
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"WARNING: Error parsing '{path}': {ex.Message}. Using default values.");
                return SimulationConfiguration.Default();
            }
            catch (ArgumentException ex)
            {
                // Validation failed - FAIL FAST
                Console.WriteLine($"ERROR: Invalid configuration in '{path}':");
                Console.WriteLine($"  {ex.Message}");
                Console.WriteLine("\nPlease fix the configuration file and restart.");
                Environment.Exit(1);
                return null!;  // Unreachable
            }
        }

        private static void Validate(SimulationConfiguration config)
        {
            var errors = new List<string>();

            if (config.MaxFloors < 2 || config.MaxFloors > 100)
                errors.Add($"MaxFloors must be between 2 and 100, got {config.MaxFloors}");

            if (config.ElevatorCount < 1 || config.ElevatorCount > 10)
                errors.Add($"ElevatorCount must be between 1 and 10, got {config.ElevatorCount}");

            if (config.TickIntervalMs < 10 || config.TickIntervalMs > 10000)
                errors.Add($"TickIntervalMs must be between 10 and 10000, got {config.TickIntervalMs}");

            if (config.DoorOpenTicks < 1 || config.DoorOpenTicks > 10)
                errors.Add($"DoorOpenTicks must be between 1 and 10, got {config.DoorOpenTicks}");

            if (config.RequestIntervalSeconds < 1 || config.RequestIntervalSeconds > 60)
                errors.Add($"RequestIntervalSeconds must be between 1 and 60, got {config.RequestIntervalSeconds}");

            if (errors.Any())
            {
                throw new ArgumentException(string.Join("\n  ", errors));
            }
        }
    }
}
