using System;
using System.Collections.Generic;
using System.Linq;
using ElevatorSystem.Infrastructure.Logging;

namespace ElevatorSystem.Common
{
    /// <summary>
    /// Rate limiter with global and per-source limits using sliding window algorithm.
    /// Thread-safe: All public methods use internal locking.
    /// </summary>
    public class RateLimiter
    {
        private readonly int _globalLimitPerMinute;
        private readonly int _perSourceLimitPerMinute;
        private readonly ILogger _logger;
        private readonly object _lock = new object();
        private readonly Queue<DateTime> _globalRequestTimestamps = new Queue<DateTime>();
        private readonly Dictionary<string, Queue<DateTime>> _perSourceRequestTimestamps = new Dictionary<string, Queue<DateTime>>();

        public RateLimiter(int globalLimitPerMinute, int perSourceLimitPerMinute, ILogger logger)
        {
            _globalLimitPerMinute = globalLimitPerMinute;
            _perSourceLimitPerMinute = perSourceLimitPerMinute;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Checks if a request from the given source is allowed.
        /// Thread-safe: Protected by internal lock.
        /// </summary>
        /// <param name="source">The source identifier (e.g., "RandomGenerator", "API")</param>
        /// <returns>True if allowed, false if rate limit exceeded</returns>
        public bool IsAllowed(string source)
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                var slidingWindowStart = now.AddMinutes(-1);

                RemoveExpiredRequests(_globalRequestTimestamps, slidingWindowStart);

                if (IsGlobalLimitExceeded())
                {
                    _logger.LogWarning($"Global rate limit exceeded: {_globalRequestTimestamps.Count} requests in last minute");
                    return false;
                }

                EnsureSourceQueueExists(source);
                var sourceQueue = _perSourceRequestTimestamps[source];
                RemoveExpiredRequests(sourceQueue, slidingWindowStart);

                if (IsPerSourceLimitExceeded(sourceQueue))
                {
                    _logger.LogWarning($"Per-source rate limit exceeded for '{source}': {sourceQueue.Count} requests");
                    return false;
                }

                RecordRequest(now, sourceQueue);
                return true;
            }
        }

        private bool IsGlobalLimitExceeded()
        {
            return _globalRequestTimestamps.Count >= _globalLimitPerMinute;
        }

        private bool IsPerSourceLimitExceeded(Queue<DateTime> sourceQueue)
        {
            return sourceQueue.Count >= _perSourceLimitPerMinute;
        }

        private void EnsureSourceQueueExists(string source)
        {
            if (!_perSourceRequestTimestamps.ContainsKey(source))
            {
                _perSourceRequestTimestamps[source] = new Queue<DateTime>();
            }
        }

        private void RecordRequest(DateTime timestamp, Queue<DateTime> sourceQueue)
        {
            _globalRequestTimestamps.Enqueue(timestamp);
            sourceQueue.Enqueue(timestamp);
        }

        private static void RemoveExpiredRequests(Queue<DateTime> queue, DateTime expirationCutoff)
        {
            while (queue.Count > 0 && queue.Peek() < expirationCutoff)
            {
                queue.Dequeue();
            }
        }
    }
}
