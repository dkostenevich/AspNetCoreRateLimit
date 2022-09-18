using System.Threading;
using System.Threading.Tasks;

namespace AspNetCoreRateLimit
{
    public class ProcessingStrategy : IProcessingStrategy
    {
        private readonly IRateLimitCounterStore _counterStore;
        private readonly IStatisticStore _statisticStore;
        private readonly IRateLimitConfiguration _config;

        public ProcessingStrategy(
            IRateLimitCounterStore counterStore, 
            IStatisticStore statisticStore, 
            IRateLimitConfiguration config
        )
        {
            _counterStore = counterStore;
            _statisticStore = statisticStore;
            _config = config;
        }

        public async Task<RateLimitCounter> ProcessRequestAsync(ClientRequestIdentity requestIdentity, RateLimitRule rule, ICounterKeyBuilder counterKeyBuilder, RateLimitOptions rateLimitOptions, CancellationToken cancellationToken = default)
        {
            var increment = _config.RateIncrementer?.Invoke() ?? 1;

            var counter = new RateLimitCounter { Timestamp = rule.GetIntervalStart() };

            var counterId = counterKeyBuilder.Build(requestIdentity, rule);

            counter = await _counterStore.IncrementAsync(counterId, increment, counter, rule.PeriodTimespan.Value, cancellationToken);

            return counter;
        }
    }
}