using System;
using System.Threading;
using System.Threading.Tasks;

namespace AspNetCoreRateLimit
{
    public interface IStatisticStore : IRateLimitStore<double?>
    {
        Task<double> IncrementAsync(string id, double increment, TimeSpan timeout, CancellationToken cancellationToken);
    }
}