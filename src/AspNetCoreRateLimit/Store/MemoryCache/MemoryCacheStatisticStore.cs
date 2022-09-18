using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace AspNetCoreRateLimit
{
    public class MemoryCacheStatisticStore : MemoryCacheStore<double?>, IStatisticStore
    {
        private static readonly AsyncKeyLock AsyncLock = new AsyncKeyLock();

        public MemoryCacheStatisticStore(IMemoryCache cache) : base(cache)
        {
        }

        public async Task<double> IncrementAsync(string id, double increment, TimeSpan timeout, CancellationToken cancellationToken)
        {
            using (await AsyncLock.WriterLockAsync(id).ConfigureAwait(false))
            {
                var value = await GetAsync(id, cancellationToken) ?? 0;

                value += increment;

                await SetAsync(id, value, timeout, cancellationToken);

                return value;
            }
        }
    }
}