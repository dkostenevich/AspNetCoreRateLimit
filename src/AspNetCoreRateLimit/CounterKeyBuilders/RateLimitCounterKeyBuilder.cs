using System.Security.Cryptography;
using System.Text;
using System;

namespace AspNetCoreRateLimit
{
    public class RateLimitCounterKeyBuilder : ICounterKeyBuilder
    {
        private readonly RateLimitOptions _options;
        private readonly ICounterKeyBuilder _counterKeyBuilder;
        private readonly IRateLimitConfiguration _configuration;

        public RateLimitCounterKeyBuilder(RateLimitOptions options, ICounterKeyBuilder counterKeyBuilder, IRateLimitConfiguration configuration)
        {
            _options = options;
            _counterKeyBuilder = counterKeyBuilder;
            _configuration = configuration;
        }

        public string Build(ClientRequestIdentity requestIdentity, RateLimitRule rule)
        {
            var key = _counterKeyBuilder.Build(requestIdentity, rule);

            if (_options.EnableEndpointRateLimiting && _configuration.EndpointCounterKeyBuilder != null)
            {
                key += _configuration.EndpointCounterKeyBuilder.Build(requestIdentity, rule);
            }

            var bytes = Encoding.UTF8.GetBytes(key);

            using var algorithm = SHA1.Create();
            var hash = algorithm.ComputeHash(bytes);

            return Convert.ToBase64String(hash);
        }
    }
}