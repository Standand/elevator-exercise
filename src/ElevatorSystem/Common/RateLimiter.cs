using System;
using System.Collections.Generic;
using System.Linq;
using ElevatorSystem.Infrastructure.Logging;

namespace ElevatorSystem.Common
{
    /// <summary>
    /// Rate limiter with global and per-source limits using sliding window.
    /// </summary>
    public class RateLimiter
    {
        private readonly int _globalLimitPerMinute;
        private readonly int _perSourceLimitPerMinute;
        private readonly ILogger _logger;
        private readonly object _lock = new object();

        private readonly Queue<DateTime> _globalRequests = new Queue<DateTime>();
        private readonly Dictionary<string, Queue<DateTime>> _sourceRequests = new Dictionary<string, Queue<DateTime>>();

        public RateLimiter(int globalLimitPerMinute, int perSourceLimitPerMinute, ILogger logger)
        {
            _globalLimitPerMinute = globalLimitPerMinute;
            _perSourceLimitPerMinute = perSourceLimitPerMinute;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Checks if a request from the given source is allowed.
        /// </summary>
        /// <param name="source">The source identifier (e.g., "RandomGenerator", "API")</param>
        /// <returns>True if allowed, false if rate limit exceeded</returns>
        public bool IsAllowed(string source)
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                var oneMinuteAgo = now.AddMinutes(-1);

                // Clean old requests
                CleanOldRequests(_globalRequests, oneMinuteAgo);

                // Check global limit
                if (_globalRequests.Count >= _globalLimitPerMinute)
                {
                    _logger.LogWarning($"Global rate limit exceeded: {_globalRequests.Count} requests in last minute");
                    return false;
                }

                // Check per-source limit
                if (!_sourceRequests.ContainsKey(source))
                    _sourceRequests[source] = new Queue<DateTime>();

                var sourceQueue = _sourceRequests[source];
                CleanOldRequests(sourceQueue, oneMinuteAgo);

                if (sourceQueue.Count >= _perSourceLimitPerMinute)
                {
                    _logger.LogWarning($"Per-source rate limit exceeded for '{source}': {sourceQueue.Count} requests");
                    return false;
                }

                // Allow request
                _globalRequests.Enqueue(now);
                sourceQueue.Enqueue(now);
                return true;
            }
        }

        private void CleanOldRequests(Queue<DateTime> queue, DateTime cutoff)
        {
            while (queue.Count > 0 && queue.Peek() < cutoff)
            {
                queue.Dequeue();
            }
        }
    }
}
