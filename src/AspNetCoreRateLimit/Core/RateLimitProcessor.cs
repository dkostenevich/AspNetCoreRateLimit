using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AspNetCoreRateLimit
{
    public abstract class RateLimitProcessor
    {
        protected IProcessingStrategy ProcessingStrategy { get; }
        protected IRateLimitCounterStore RateLimitCounterStore { get; }
        protected RateLimitOptions Options { get; }

        protected RateLimitProcessor(
            RateLimitOptions options,
            IProcessingStrategy processingStrategy,
            IRateLimitCounterStore rateLimitCounterStore
        )
        {
            ProcessingStrategy = processingStrategy;
            RateLimitCounterStore = rateLimitCounterStore;
            Options = options;
        }

        protected abstract ICounterKeyBuilder CounterKeyBuilder { get; }

        public abstract Task<IEnumerable<RateLimitRule>> GetMatchingRulesAsync(ClientRequestIdentity identity,
            CancellationToken cancellationToken = default);

        public async Task<RateLimitCounter> ProcessRequestAsync(ClientRequestIdentity requestIdentity, RateLimitRule rule, CancellationToken cancellationToken = default)
        {
            return await ProcessingStrategy.ProcessRequestAsync(requestIdentity, rule, CounterKeyBuilder, Options, cancellationToken);
        }

        public virtual bool IsWhitelisted(ClientRequestIdentity requestIdentity)
        {
            if (Options.ClientWhitelist != null && Options.ClientWhitelist.Contains(requestIdentity.ClientId))
            {
                return true;
            }

            if (Options.IpWhitelist != null && IpParser.ContainsIp(Options.IpWhitelist, requestIdentity.ClientIp))
            {
                return true;
            }

            if (Options.EndpointWhitelist != null && Options.EndpointWhitelist.Any())
            {
                string path = Options.EnableRegexRuleMatching ? $".+:{requestIdentity.Path}" : $"*:{requestIdentity.Path}";

                if (Options.EndpointWhitelist.Any(x => $"{requestIdentity.HttpVerb}:{requestIdentity.Path}".IsUrlMatch(x, Options.EnableRegexRuleMatching)) ||
                        Options.EndpointWhitelist.Any(x => path.IsUrlMatch(x, Options.EnableRegexRuleMatching)))
                    return true;
            }

            return false;
        }

        public virtual RateLimitHeaders GetRateLimitHeaders(RateLimitCounter? counter, RateLimitRule rule, CancellationToken cancellationToken = default)
        {
            var headers = new RateLimitHeaders();

            double remaining;
            DateTime reset;

            if (counter.HasValue)
            {
                reset = counter.Value.Timestamp + (rule.PeriodTimespan ?? rule.Period.ToTimeSpan());
                remaining = rule.Limit - counter.Value.Count;
            }
            else
            {
                var intervalStart = rule.GetIntervalStart();
                reset = intervalStart + (rule.PeriodTimespan ?? rule.Period.ToTimeSpan());
                remaining = rule.Limit;
            }

            headers.Reset = reset.ToUniversalTime().ToString("o", DateTimeFormatInfo.InvariantInfo);
            headers.Limit = rule.Period;
            headers.Remaining = remaining.ToString();

            return headers;
        }

        protected virtual List<RateLimitRule> GetMatchingRules(ClientRequestIdentity identity, List<RateLimitRule> rules = null)
        {
            var limits = new List<RateLimitRule>();

            if (rules?.Any() == true)
            {
                if (Options.EnableEndpointRateLimiting)
                {
                    // search for rules with endpoints like "*" and "*:/matching_path"

                    string path = Options.EnableRegexRuleMatching ? $".+:{identity.Path}" : $"*:{identity.Path}";

                    var pathLimits = rules.Where(r => path.IsUrlMatch(r.Endpoint, Options.EnableRegexRuleMatching));
                    limits.AddRange(pathLimits);

                    // search for rules with endpoints like "matching_verb:/matching_path"
                    var verbLimits = rules.Where(r => $"{identity.HttpVerb}:{identity.Path}".IsUrlMatch(r.Endpoint, Options.EnableRegexRuleMatching));
                    limits.AddRange(verbLimits);
                }
                else
                {
                    // ignore endpoint rules and search for global rules only
                    var genericLimits = rules.Where(r => r.Endpoint == "*");
                    limits.AddRange(genericLimits);
                }

                // get the most restrictive limit for each period 
                limits = limits.GroupBy(l => l.Period).Select(l => l.OrderBy(x => x.Limit)).Select(l => l.First()).ToList();
            }

            // search for matching general rules
            if (Options.GeneralRules != null)
            {
                var matchingGeneralLimits = new List<RateLimitRule>();

                if (Options.EnableEndpointRateLimiting)
                {
                    // search for rules with endpoints like "*" and "*:/matching_path" in general rules
                    var pathLimits = Options.GeneralRules.Where(r => $"*:{identity.Path}".IsUrlMatch(r.Endpoint, Options.EnableRegexRuleMatching));
                    matchingGeneralLimits.AddRange(pathLimits);

                    // search for rules with endpoints like "matching_verb:/matching_path" in general rules
                    var verbLimits = Options.GeneralRules.Where(r => $"{identity.HttpVerb}:{identity.Path}".IsUrlMatch(r.Endpoint, Options.EnableRegexRuleMatching));
                    matchingGeneralLimits.AddRange(verbLimits);
                }
                else
                {
                    //ignore endpoint rules and search for global rules in general rules
                    var genericLimits = Options.GeneralRules.Where(r => r.Endpoint == "*");
                    matchingGeneralLimits.AddRange(genericLimits);
                }

                // get the most restrictive general limit for each period 
                var generalLimits = matchingGeneralLimits
                    .GroupBy(l => l.Period)
                    .Select(l => l.OrderBy(x => x.Limit).ThenBy(x => x.Endpoint))
                    .Select(l => l.First())
                    .ToList();

                foreach (var generalLimit in generalLimits)
                {
                    // add general rule if no specific rule is declared for the specified period
                    if (!limits.Exists(l => l.Period == generalLimit.Period))
                    {
                        limits.Add(generalLimit);
                    }
                }
            }

            foreach (var item in limits)
            {
                if (!item.PeriodTimespan.HasValue)
                {
                    // parse period text into time spans	
                    item.PeriodTimespan = item.Period.ToTimeSpan();
                }
            }

            limits = limits.OrderBy(l => l.PeriodTimespan).ToList();

            if (Options.StackBlockedRequests)
            {
                limits.Reverse();
            }

            return limits;
        }

        public async Task ResetLimitsAsync(ClientRequestIdentity identity, CancellationToken cancellationToken = default)
        {
            var rules = await GetMatchingRulesAsync(identity, cancellationToken);
            foreach (var rule in rules)
            {
                var key = CounterKeyBuilder.Build(identity, rule);
                await RateLimitCounterStore.RemoveAsync(key, cancellationToken);
            }
        }
    }
}
