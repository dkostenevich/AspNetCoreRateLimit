using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace AspNetCoreRateLimit.Resolvers
{
    public class ClientRequestIdentityResolver: IClientRequestIdentityResolver
    {
        private readonly IRateLimitConfiguration _config;

        public ClientRequestIdentityResolver(IRateLimitConfiguration config)
        {
            _config = config;
            _config.RegisterResolvers();
        }

        public async Task<ClientRequestIdentity> Resolve(HttpContext context)
        {
            string clientIp = null;
            string clientId = null;

            if (_config.ClientResolvers?.Any() == true)
            {
                foreach (var resolver in _config.ClientResolvers)
                {
                    clientId = await resolver.ResolveClientAsync(context);

                    if (!string.IsNullOrEmpty(clientId))
                    {
                        break;
                    }
                }
            }

            if (_config.IpResolvers?.Any() == true)
            {
                foreach (var resolver in _config.IpResolvers)
                {
                    clientIp = resolver.ResolveIp(context);

                    if (!string.IsNullOrEmpty(clientIp))
                    {
                        break;
                    }
                }
            }
            var path = context.Request.Path.ToString().ToLowerInvariant();
            return new ClientRequestIdentity
            {
                ClientIp = clientIp,
                Path = path == "/"
                    ? path
                    : path.TrimEnd('/'),
                HttpVerb = context.Request.Method.ToLowerInvariant(),
                ClientId = clientId ?? "anon"
            };
        }
    }
}
