using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text;

namespace mysystem_bff.Services.Services.ApiUsage;

public class EndpointUsageTracker
{
    private readonly ConcurrentDictionary<string, int> _counts = new();

    public void Record(HttpContext context)
    {
        var username =
            context.User.FindFirstValue(ClaimTypes.Name)
            ?? "anonymous";

        var endpoint =
            $"{context.Request.Scheme}://" +
            $"{context.Request.Host}" +
            $"{context.Request.Path}" +
            $"{context.Request.QueryString}";

        var key = $"{username}\u001F{endpoint}";

        _counts.AddOrUpdate(
            key,
            1,
            (_, currentCount) => currentCount + 1);
    }

    public async Task WriteCsvAsync(
        string directory,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(directory);

        var date = DateTime.Now.ToString("yyyy-MM-dd");
        var filePath = Path.Combine(
            directory,
            $"endpoint-usage-{date}.csv");

        var rows = _counts
            .Select(entry =>
            {
                var parts = entry.Key.Split('\u001F', 2);

                return new
                {
                    User = parts[0],
                    Endpoint = parts.Length > 1 ? parts[1] : "",
                    Count = entry.Value
                };
            })
            .OrderByDescending(row => row.Count)
            .ThenBy(row => row.User)
            .ThenBy(row => row.Endpoint)
            .ToList();

        var csv = new StringBuilder();

        csv.AppendLine("User,Endpoint,Times Requested");

        foreach (var row in rows)
        {
            csv.AppendLine(
                $"{EscapeCsv(row.User)}," +
                $"{EscapeCsv(row.Endpoint)}," +
                $"{row.Count}");
        }

        await File.WriteAllTextAsync(
            filePath,
            csv.ToString(),
            cancellationToken);
    }

    private static string EscapeCsv(string value)
    {
        var escaped = value.Replace("\"", "\"\"");

        return $"\"{escaped}\"";
    }
}