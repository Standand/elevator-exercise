using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ElevatorSystem.Infrastructure.Logging;
using ElevatorSystem.Infrastructure.Metrics;

namespace ElevatorSystem.Application.Services
{
    /// <summary>
    /// Orchestrates the simulation and request generator services.
    /// </summary>
    public class SystemOrchestrator
    {
        private readonly ElevatorSimulationService _simulationService;
        private readonly RandomRequestGenerator _requestGenerator;
        private readonly IMetrics _metrics;
        private readonly ILogger _logger;

        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _simulationTask;
        private Task? _generatorTask;
        private Task? _metricsTask;

        public SystemOrchestrator(
            ElevatorSimulationService simulationService,
            RandomRequestGenerator requestGenerator,
            IMetrics metrics,
            ILogger logger)
        {
            _simulationService = simulationService ?? throw new ArgumentNullException(nameof(simulationService));
            _requestGenerator = requestGenerator ?? throw new ArgumentNullException(nameof(requestGenerator));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Starts all services.
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = _cancellationTokenSource.Token;

            _logger.LogInfo("Starting system...");

            _simulationTask = _simulationService.RunAsync(token);
            _generatorTask = _requestGenerator.RunAsync(token);
            _metricsTask = StartMetricsReporter(token);

            _logger.LogInfo("System started");

            await Task.WhenAll(_simulationTask, _generatorTask, _metricsTask);
        }

        private Task StartMetricsReporter(CancellationToken token)
        {
            return Task.Run(async () =>
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        await Task.Delay(10000, token);
                        LogMetrics();
                    }
                }
                catch (OperationCanceledException)
                {
                }
            }, token);
        }

        private void LogMetrics()
        {
            var snapshot = _metrics.GetSnapshot();
            _logger.LogInfo($"[METRICS] Requests: {snapshot.TotalRequests} total " +
                           $"({snapshot.AcceptedRequests} accepted, {snapshot.RejectedRequests} rejected) | " +
                           $"Completed: {snapshot.CompletedHallCalls} | " +
                           $"Pending: {snapshot.PendingHallCalls} | " +
                           $"Active Elevators: {snapshot.ActiveElevators}");
        }

        /// <summary>
        /// Shuts down all services gracefully.
        /// </summary>
        public void Shutdown()
        {
            _logger.LogInfo("Shutdown initiated");
            _cancellationTokenSource?.Cancel();
            _logger.LogInfo("Cancellation signal sent");
        }
    }
}
