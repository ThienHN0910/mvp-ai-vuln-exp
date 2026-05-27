using System.Collections.Concurrent;

namespace Backend.Middleware;

/// <summary>
/// Rate limiter middleware for Gemini API calls.
/// Enforces: 15 requests per minute, 500 requests per day.
/// </summary>
public class GeminiRateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GeminiRateLimitMiddleware> _logger;

    private static readonly ConcurrentDictionary<string, RateLimitBucket> _buckets = new();
    private const int RequestsPerMinute = 15;
    private const int RequestsPerDay = 500;

    public GeminiRateLimitMiddleware(RequestDelegate next, ILogger<GeminiRateLimitMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only apply rate limiting to Gemini analyze and webhook endpoints
        if (!IsGeminiEndpoint(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var clientId = GetClientIdentifier(context);
        var bucket = _buckets.AddOrUpdate(
            clientId,
            _ => new RateLimitBucket(),
            (_, existing) => existing
        );

        string? rateLimitError = null;
        int? retryAfter = null;

        lock (bucket)
        {
            bucket.CleanupOldEntries();

            // Check per-minute limit
            var minuteCount = bucket.RequestsInLastMinute();
            if (minuteCount >= RequestsPerMinute)
            {
                _logger.LogWarning(
                    "Rate limit exceeded (per-minute): {ClientId} has {Count}/{Limit} requests in last minute.",
                    clientId, minuteCount, RequestsPerMinute);

                rateLimitError = "Rate limit exceeded (15 requests per minute).";
                retryAfter = 60;
            }
            // Check per-day limit
            else if (bucket.RequestsInLastDay() >= RequestsPerDay)
            {
                _logger.LogWarning(
                    "Rate limit exceeded (per-day): {ClientId} has {Count}/{Limit} requests in last day.",
                    clientId, bucket.RequestsInLastDay(), RequestsPerDay);

                rateLimitError = "Rate limit exceeded (500 requests per day).";
                retryAfter = 86400;
            }
            else
            {
                // Record this request
                bucket.RecordRequest();
            }
        }

        if (rateLimitError is not null)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.Append("Retry-After", retryAfter.ToString() ?? "60");
            await context.Response.WriteAsJsonAsync(new
            {
                message = rateLimitError,
                retryAfter = retryAfter
            });
            return;
        }

        await _next(context);
    }

    private static bool IsGeminiEndpoint(PathString path)
    {
        var pathStr = path.Value ?? "";
        return pathStr.Contains("/api/analyze", StringComparison.OrdinalIgnoreCase) ||
               pathStr.Contains("/api/webhook", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetClientIdentifier(HttpContext context)
    {
        // Use IP address as client identifier (can be extended with API keys if needed)
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private sealed class RateLimitBucket
    {
        private readonly List<DateTime> _requestTimes = new();

        public void RecordRequest()
        {
            _requestTimes.Add(DateTime.UtcNow);
        }

        public int RequestsInLastMinute()
        {
            var oneMinuteAgo = DateTime.UtcNow.AddMinutes(-1);
            return _requestTimes.Count(t => t > oneMinuteAgo);
        }

        public int RequestsInLastDay()
        {
            var oneDayAgo = DateTime.UtcNow.AddDays(-1);
            return _requestTimes.Count(t => t > oneDayAgo);
        }

        public void CleanupOldEntries()
        {
            var twoDaysAgo = DateTime.UtcNow.AddDays(-2);
            _requestTimes.RemoveAll(t => t < twoDaysAgo);
        }
    }
}

/// <summary>
/// Extension method to register the rate limit middleware.
/// </summary>
public static class GeminiRateLimitMiddlewareExtensions
{
    public static IApplicationBuilder UseGeminiRateLimit(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<GeminiRateLimitMiddleware>();
    }
}
