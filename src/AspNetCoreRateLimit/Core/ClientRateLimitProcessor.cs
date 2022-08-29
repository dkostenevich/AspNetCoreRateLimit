using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AspNetCoreRateLimit.Core;
using Microsoft.Extensions.Options;

namespace AspNetCoreRateLimit
{
    public class ClientRateLimitProcessor : RateLimitProcessor, IClientRateLimitProcessor
    {
        private readonly ClientRateLimitOptions _options;
        private readonly IRateLimitStore<ClientRateLimitPolicy> _policyStore;

        public ClientRateLimitProcessor(
                IOptions<ClientRateLimitOptions> options,
                IClientPolicyStore policyStore,
                IProcessingStrategy processingStrategy,
                IRateLimitCounterStore rateLimitCounterStore,
                IRateLimitConfiguration configuration)
            : base(options.Value, processingStrategy, rateLimitCounterStore)
        {
            _options = options.Value;
            _policyStore = policyStore;

            CounterKeyBuilder = new RateLimitCounterKeyBuilder(Options, new ClientCounterKeyBuilder(options.Value), configuration);
        }

        protected override ICounterKeyBuilder CounterKeyBuilder { get; }

        public override async Task<IEnumerable<RateLimitRule>> GetMatchingRulesAsync(ClientRequestIdentity identity, CancellationToken cancellationToken = default)
        {
            var policy = await _policyStore.GetAsync($"{_options.ClientPolicyPrefix}_{identity.ClientId}", cancellationToken);

            return GetMatchingRules(identity, policy?.Rules);
        }
    }
}