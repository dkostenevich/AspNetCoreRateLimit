using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AspNetCoreRateLimit.Core;
using Microsoft.Extensions.Options;

namespace AspNetCoreRateLimit
{
    public class IpRateLimitProcessor : RateLimitProcessor, IIpRateLimitProcessor
    {
        private readonly IpRateLimitOptions _options;
        private readonly IRateLimitStore<IpRateLimitPolicies> _policyStore;

        public IpRateLimitProcessor(
                IOptions<IpRateLimitOptions> options,
                IIpPolicyStore policyStore,
                IProcessingStrategy processingStrategy,
                IRateLimitCounterStore rateLimitCounterStore,
                IRateLimitConfiguration configuration)
            : base(
                options.Value,
                processingStrategy,
                rateLimitCounterStore
            )
        {
            _options = options.Value;
            _policyStore = policyStore;

            CounterKeyBuilder = new RateLimitCounterKeyBuilder(options.Value, new IpCounterKeyBuilder(options.Value), configuration);
        }

        protected override ICounterKeyBuilder CounterKeyBuilder { get; }

        public override async Task<IEnumerable<RateLimitRule>> GetMatchingRulesAsync(ClientRequestIdentity identity, CancellationToken cancellationToken = default)
        {
            var policies = await _policyStore.GetAsync($"{_options.IpPolicyPrefix}", cancellationToken);

            var rules = new List<RateLimitRule>();

            if (policies?.IpRules?.Any() == true)
            {
                // search for rules with IP intervals containing client IP
                var matchPolicies = policies.IpRules.Where(r => IpParser.ContainsIp(r.Ip, identity.ClientIp));

                foreach (var item in matchPolicies)
                {
                    rules.AddRange(item.Rules);
                }
            }

            return GetMatchingRules(identity, rules);
        }
    }
}