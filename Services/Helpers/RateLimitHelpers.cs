using System.Security.Claims;

namespace mysystem_bff.Services.Helpers
{
    public class RateLimitHelpers
    {
        public static string GetRateLimitPartitionKey(
            HttpContext context)
        {
            var userId = 
                context.User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (!string.IsNullOrWhiteSpace(userId))
                return $"user:{userId}";

            var ipAddress = context.Connection
                .RemoteIpAddress?
                .ToString();

            return $"ip:{ipAddress ?? "Unknown"}";
        }
    }
}
