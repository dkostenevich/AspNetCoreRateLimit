using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AspNetCoreRateLimit.Redis
{
    public class RedisProcessingStrategy : IProcessingStrategy
    {
        private readonly IConnectionMultiplexer _connectionMultiplexer;
        private readonly IRateLimitConfiguration _config;
        private readonly ILogger<RedisProcessingStrategy> _logger;

        public RedisProcessingStrategy(
            IConnectionMultiplexer connectionMultiplexer,
            IRateLimitConfiguration config,
            ILogger<RedisProcessingStrategy> logger
        )
        {
            _connectionMultiplexer = connectionMultiplexer ?? throw new ArgumentException("IConnectionMultiplexer was null. Ensure StackExchange.Redis was successfully registered");
            _config = config;
            _logger = logger;
        }

        private static readonly LuaScript AtomicIncrement = LuaScript.Prepare("local count = redis.call(\"INCRBYFLOAT\", @key, tonumber(@delta)) local ttl = redis.call(\"TTL\", @key) if ttl == -1 then redis.call(\"EXPIRE\", @key, @timeout) end return count");

        public async Task<RateLimitCounter> ProcessRequestAsync(ClientRequestIdentity requestIdentity, RateLimitRule rule, ICounterKeyBuilder counterKeyBuilder, RateLimitOptions rateLimitOptions, CancellationToken cancellationToken = default)
        {
            var counterId = counterKeyBuilder.Build(requestIdentity, rule);
            return await IncrementAsync(counterId, rule, _config.RateIncrementer);
        }

        public async Task<RateLimitCounter> IncrementAsync(string counterId, RateLimitRule rule, Func<double> rateIncrementer = null)
        {
            var interval = rule.PeriodTimespan.Value;
            var intervalStart = rule.GetIntervalStart();

            _logger.LogDebug("Calling Lua script. {counterId}, {timeout}, {delta}", counterId, interval.TotalSeconds, 1D);
            var count = await _connectionMultiplexer.GetDatabase().ScriptEvaluateAsync(AtomicIncrement, new { key = new RedisKey(counterId), timeout = interval.TotalSeconds, delta = rateIncrementer?.Invoke() ?? 1D });
            return new RateLimitCounter
            {
                Count = (double)count,
                Timestamp = intervalStart
            };
        }
    }
}
