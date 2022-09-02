using AspNetCoreRateLimit.Core;
using AspNetCoreRateLimit.Resolvers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AspNetCoreRateLimit
{
    public class ClientRateLimitMiddleware : RateLimitMiddleware
    {
        private readonly IClientRateLimitProcessor _clientRateLimitProcessor;
        private readonly ILogger<ClientRateLimitMiddleware> _logger;

        public ClientRateLimitMiddleware(RequestDelegate next,
            IOptions<ClientRateLimitOptions> options,
            IClientRateLimitProcessor clientRateLimitProcessor,
            IClientRequestIdentityResolver clientRequestIdentityResolver,
            ILogger<ClientRateLimitMiddleware> logger)
        : base(next, options?.Value, clientRequestIdentityResolver)
        {
            _clientRateLimitProcessor = clientRateLimitProcessor;
            _logger = logger;
        }

        protected override IRateLimitProcessor GetProcessor(ClientRequestIdentity identity) => _clientRateLimitProcessor;

        protected override void LogBlockedRequest(HttpContext httpContext, ClientRequestIdentity identity, RateLimitCounter counter, RateLimitRule rule)
        {
            _logger.LogInformation($"Request {identity.HttpVerb}:{identity.Path} from ClientId {identity.ClientId} has been blocked, quota {rule.Limit}/{rule.Period} exceeded by {counter.Count - rule.Limit}. Blocked by rule {rule.Endpoint}, TraceIdentifier {httpContext.TraceIdentifier}. MonitorMode: {rule.MonitorMode}");
        }
    }
}