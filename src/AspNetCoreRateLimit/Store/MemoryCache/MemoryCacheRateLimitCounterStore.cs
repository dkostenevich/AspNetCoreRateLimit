﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace AspNetCoreRateLimit
{
    public class MemoryCacheRateLimitCounterStore : MemoryCacheStore<RateLimitCounter?>, IRateLimitCounterStore
    {
        private static readonly AsyncKeyLock AsyncLock = new AsyncKeyLock();

        public MemoryCacheRateLimitCounterStore(IMemoryCache cache) : base(cache)
        {
        }

        public async Task<RateLimitCounter> IncrementAsync(string id, double increment, RateLimitCounter initial, TimeSpan timeout, CancellationToken cancellationToken)
        {
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