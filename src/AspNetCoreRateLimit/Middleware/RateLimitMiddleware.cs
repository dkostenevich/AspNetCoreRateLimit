using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AspNetCoreRateLimit.Resolvers;
using Microsoft.AspNetCore.Http;

namespace AspNetCoreRateLimit
{
    public abstract class RateLimitMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly RateLimitOptions _options;
        private readonly IClientRequestIdentityResolver _identityResolver;

        protected RateLimitMiddleware(
            RequestDelegate next,
            RateLimitOptions options,
            IClientRequestIdentityResolver identityResolver)
        {
            _next = next;
            _options = options;
            _identityResolver = identityResolver;
        }

        protected abstract IRateLimitProcessor GetProcessor(ClientRequestIdentity identity);

        public async Task Invoke(HttpContext context)
        {
            // check if rate limiting is enabled
            if (_options == null)
            {
                await _next.Invoke(context);
                return;
            }

            // compute identity from request
            var identity = await _identityResolver.Resolve(context);
            var processor = GetProcessor(identity);

            // check white list
            if (processor.IsWhitelisted(identity))
            {
                await _next.Invoke(context);
                return;
            }

            var rules = await processor.GetMatchingRulesAsync(identity, context.RequestAborted);

            var rulesDict = new Dictionary<RateLimitRule, RateLimitCounter>();

            foreach (var rule in rules)
            {
                // increment counter
                var rateLimitCounter = await processor.ProcessRequestAsync(identity, rule, context.RequestAborted);

                if (rule.Limit > 0)
                {
                    // check if key expired
                    if (rateLimitCounter.Timestamp + rule.PeriodTimespan.Value < DateTime.UtcNow)
                    {
                        continue;
                    }

                    // check if limit is reached
                    if (rateLimitCounter.Count > rule.Limit)
                    {
                        //compute retry after value
                        var retryAfter = rateLimitCounter.Timestamp.RetryAfterFrom(rule);

                        // log blocked request
                        LogBlockedRequest(context, identity, rateLimitCounter, rule);

                        if (_options.RequestBlockedBehaviorAsync != null)
                        {
                            await _options.RequestBlockedBehaviorAsync(context, identity, rateLimitCounter, rule);
                        }

                        if (!rule.MonitorMode)
                        {
                            // break execution
                            await ReturnQuotaExceededResponse(context, rule, retryAfter);

                            return;
                        }
                    }
                }
                // if limit is zero or less, block the request.
                else
                {
                    // log blocked request
                    LogBlockedRequest(context, identity, rateLimitCounter, rule);

                    if (_options.RequestBlockedBehaviorAsync != null)
                    {
                        await _options.RequestBlockedBehaviorAsync(context, identity, rateLimitCounter, rule);
                    }

                    if (!rule.MonitorMode)
                    {
                        // break execution (Int32 max used to represent infinity)
                        await ReturnQuotaExceededResponse(context, rule, int.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture));

                        return;
                    }
                }

                rulesDict.Add(rule, rateLimitCounter);
            }

            // set X-Rate-Limit headers for the longest period
            if (rulesDict.Any() && !_options.DisableRateLimitHeaders)
            {
                var rule = rulesDict.OrderByDescending(x => x.Key.PeriodTimespan).FirstOrDefault();
                var headers = processor.GetRateLimitHeaders(rule.Value, rule.Key, context.RequestAborted);

                headers.Context = context;

                context.Response.OnStarting(SetRateLimitHeaders, state: headers);
            }

            await _next.Invoke(context);
        }

        public virtual Task ReturnQuotaExceededResponse(HttpContext httpContext, RateLimitRule rule, string retryAfter)
        {
            //Use Endpoint QuotaExceededResponse
            if (rule.QuotaExceededResponse != null)
            {
                _options.QuotaExceededResponse = rule.QuotaExceededResponse;
            }
            var message = string.Format(
                _options.QuotaExceededResponse?.Content ??
                _options.QuotaExceededMessage ??
                "API calls quota exceeded! maximum admitted {0} per {1}.",
                rule.Limit,
                rule.PeriodTimespan.HasValue ? FormatPeriodTimespan(rule.PeriodTimespan.Value) : rule.Period, retryAfter);
            if (!_options.DisableRateLimitHeaders)
            {
                httpContext.Response.Headers["Retry-After"] = retryAfter;
            }

            httpContext.Response.StatusCode = _options.QuotaExceededResponse?.StatusCode ?? _options.HttpStatusCode;
            httpContext.Response.ContentType = _options.QuotaExceededResponse?.ContentType ?? "text/plain";

            return httpContext.Response.WriteAsync(message);
        }

        private static string FormatPeriodTimespan(TimeSpan period)
        {
            var sb = new StringBuilder();

            if (period.Days > 0)
            {
                sb.Append($"{period.Days}d");
            }

            if (period.Hours > 0)
            {
                sb.Append($"{period.Hours}h");
            }

            if (period.Minutes > 0)
            {
                sb.Append($"{period.Minutes}m");
            }

            if (period.Seconds > 0)
            {
                sb.Append($"{period.Seconds}s");
            }

            if (period.Milliseconds > 0)
            {
                sb.Append($"{period.Milliseconds}ms");
            }

            return sb.ToString();
        }

        protected abstract void LogBlockedRequest(HttpContext httpContext, ClientRequestIdentity identity, RateLimitCounter counter, RateLimitRule rule);

        private Task SetRateLimitHeaders(object rateLimitHeaders)
        {
            var headers = (RateLimitHeaders)rateLimitHeaders;

            headers.Context.Response.Headers["X-Rate-Limit-Limit"] = headers.Limit;
            headers.Context.Response.Headers["X-Rate-Limit-Remaining"] = headers.Remaining;
            headers.Context.Response.Headers["X-Rate-Limit-Reset"] = headers.Reset;

            return Task.CompletedTask;
        }
    }
}