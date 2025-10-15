namespace DeJargonizer2025.Helpers
{
    public static class IpHelper
    {
        public static string? GetClientIp(HttpContext ctx)
        {
            if (ctx.Request.Headers.TryGetValue("CF-Connecting-IP", out var cfIp) &&
                !string.IsNullOrWhiteSpace(cfIp))
                return cfIp.ToString();

            if (ctx.Request.Headers.TryGetValue("X-Forwarded-For", out var xff) &&
                !string.IsNullOrWhiteSpace(xff))
            {
                var first = xff.ToString().Split(',')[0].Trim();
                if (!string.IsNullOrWhiteSpace(first)) return first;
            }

            return ctx.Connection.RemoteIpAddress?.ToString();
        }
    }
}
