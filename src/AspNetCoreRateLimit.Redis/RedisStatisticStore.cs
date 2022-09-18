using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

namespace AspNetCoreRateLimit.Redis
{
    public class RedisStatisticStore : DistributedCacheStore<double?>, IStatisticStore
    {
        private readonly IConnectionMultiplexer _connectionMultiplexer;

        private static readonly LuaScript AtomicIncrement = LuaScript.Prepare("local count = redis.call(\"INCRBYFLOAT\", @key, tonumber(@delta)) local ttl = redis.call(\"TTL\", @key) if ttl == -1 then redis.call(\"EXPIRE\", @key, @timeout) end return count");

        public RedisStatisticStore(
            IDistributedCache cache,
            IConnectionMultiplexer connectionMultiplexer
            ) : base(cache)
        {
            _connectionMultiplexer = connectionMultiplexer ?? throw new ArgumentException("IConnectionMultiplexer was null. Ensure StackExchange.Redis was successfully registered");
        }

        public async Task<double> IncrementAsync(string id, double increment, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var count = await _connectionMultiplexer.GetDatabase().ScriptEvaluateAsync(AtomicIncrement, new { key = new RedisKey(id), timeout, delta = increment });
            return (double)count;
        }
    }
}
