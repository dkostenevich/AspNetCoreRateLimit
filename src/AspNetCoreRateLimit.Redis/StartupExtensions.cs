using Microsoft.Extensions.DependencyInjection;

namespace AspNetCoreRateLimit.Redis
{
    public static class StartupExtensions
    {
        public static IServiceCollection AddRedisRateLimiting(this IServiceCollection services)
        {
            services.AddSingleton<IIpPolicyStore, DistributedCacheIpPolicyStore>();
            services.AddSingleton<IClientPolicyStore, DistributedCacheClientPolicyStore>();
            services.AddSingleton<IRateLimitCounterStore, RedisRateLimitCounterStore>();
            services.AddSingleton<IStatisticStore, RedisStatisticStore>();
            services.AddSingleton<IProcessingStrategy, ProcessingStrategy>();
            return services;
        }
    }
}