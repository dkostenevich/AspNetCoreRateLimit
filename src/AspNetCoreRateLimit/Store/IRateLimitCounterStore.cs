using System;
using System.Threading;
using System.Threading.Tasks;

namespace AspNetCoreRateLimit
{
    public interface IRateLimitCounterStore : IRateLimitStore<RateLimitCounter?>
    {
        Task<RateLimitCounter> IncrementAsync(string id, double increment, RateLimitCounter initial, TimeSpan timeout, CancellationToken cancellationToken);
    }
}