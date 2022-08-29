using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace AspNetCoreRateLimit.Resolvers
{
    public interface IClientRequestIdentityResolver
    {
        Task<ClientRequestIdentity> Resolve(HttpContext context);
    }
}
