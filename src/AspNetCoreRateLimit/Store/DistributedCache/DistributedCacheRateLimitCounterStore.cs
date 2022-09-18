using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;

namespace AspNetCoreRateLimit
{
    public class DistributedCacheRateLimitCounterStore : DistributedCacheStore<RateLimitCounter?>, IRateLimitCounterStore
    {
        private static readonly AsyncKeyLock AsyncLock = new AsyncKeyLock();

        public DistributedCacheRateLimitCounterStore(IDistributedCache cache) : base(cache)
        {
        }

        public async Task<RateLimitCounter> IncrementAsync(string id, double increment, RateLimitCounter initial, TimeSpan timeout, CancellationToken cancellationToken)
        {
            // TODO use distributed lock in future 

            using (await AsyncLock.WriterLockAsync(id).ConfigureAwait(false))
            {
                var value = await GetAsync(id, cancellationToken) ?? initial;

                value.Count += increment;

                await SetAsync(id, value, timeout, cancellationToken);

                return value;
            }
        }
    }
}