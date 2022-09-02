using System;
using System.Text.RegularExpressions;

namespace AspNetCoreRateLimit
{
    public static class Extensions
    {
        private static readonly Regex PeriodValueRegex = new Regex(@"^\d+");
        private static readonly Regex PeriodTypeRegex = new Regex(@"[a-z]+$");

        public static bool IsUrlMatch(this string source, string value, bool useRegex)
        {
            if (useRegex)
            {
                return IsRegexMatch(source, value);
            }
            return source.IsWildCardMatch(value);
        }

        public static bool IsWildCardMatch(this string source, string value)
        {
            return source != null && value != null && source.ToLowerInvariant().IsMatch(value.ToLowerInvariant());
        }

        public static bool IsRegexMatch(this string source, string value)
        {
            if (source == null || string.IsNullOrEmpty(value))
            {
                return false;
            }
            // if the regex is e.g. /api/values/ the path should be an exact match
            // if all paths below this should be included the regex should be /api/values/*
            if (value[value.Length - 1] != '$')
            {
                value += '$';
            }
            if (value[0] != '^')
            {
                value = '^' + value;
            }
            return Regex.IsMatch(source, value, RegexOptions.IgnoreCase);
        }

        public static string RetryAfterFrom(this DateTime timestamp, RateLimitRule rule)
        {
            var diff = timestamp + rule.PeriodTimespan.Value - DateTime.UtcNow;
            var seconds = Math.Max(diff.TotalSeconds, 1);

            return $"{seconds:F0}";
        }

        public static TimeSpan ToTimeSpan(this string timeSpan)
        {
            var value = PeriodValueRegex.Match(timeSpan).Value;
            var type = PeriodTypeRegex.Match(timeSpan).Value;
            var parsedValue = double.Parse(value);

            return type switch
            {
                "y" => TimeSpan.FromDays(parsedValue * 365),
                "mo" => TimeSpan.FromDays(parsedValue * 30),
                "w" => TimeSpan.FromDays(parsedValue * 7),
                "d" => TimeSpan.FromDays(parsedValue),
                "h" => TimeSpan.FromHours(parsedValue),
                "m" => TimeSpan.FromMinutes(parsedValue),
                "s" => TimeSpan.FromSeconds(parsedValue),
                _ => throw new FormatException($"{timeSpan} can't be converted to TimeSpan, unknown type {type}"),
            };
        }
    }
}