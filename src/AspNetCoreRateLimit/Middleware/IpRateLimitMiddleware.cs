using AspNetCoreRateLimit.Core;
using AspNetCoreRateLimit.Resolvers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AspNetCoreRateLimit
{
    public class IpRateLimitMiddleware : RateLimitMiddleware
    {
        private readonly ILogger<IpRateLimitMiddleware> _logger;

        public IpRateLimitMiddleware(RequestDelegate next,
            IOptions<IpRateLimitOptions> options,
            IIpRateLimitProcessor rateLimitProcessor,
            IClientRequestIdentityResolver clientRequestIdentityResolver,
            ILogger<IpRateLimitMiddleware> logger
        )
            : base(next, options?.Value, clientRequestIdentityResolver, rateLimitProcessor)
        {
            _logger = logger;
        }

        protected override void LogBlockedRequest(HttpContext httpContext, ClientRequestIdentity identity, RateLimitCounter counter, RateLimitRule rule)
        {
            _logger.LogInformation($"Request {identity.HttpVerb}:{identity.Path} from IP {identity.ClientIp} has been blocked, quota {rule.Limit}/{rule.Period} exceeded by {counter.Count - rule.Limit}. Blocked by rule {rule.Endpoint}, TraceIdentifier {httpContext.TraceIdentifier}. MonitorMode: {rule.MonitorMode}");
        }
    }
}