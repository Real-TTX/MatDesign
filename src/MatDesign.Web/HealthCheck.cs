using System.Text.RegularExpressions;

namespace MatDesign.Web;

/// <summary>Docker <c>HEALTHCHECK</c> without curl: the app asks its own <c>/health</c> and answers with an exit code.</summary>
public static partial class HealthCheck
{
    [GeneratedRegex(@":(\d+)")]
    private static partial Regex PortRegex();

    public static async Task<int> RunAsync()
    {
        var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
        var match = PortRegex().Match(urls ?? string.Empty);
        var port = match.Success ? match.Groups[1].Value : "8060";

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await http.GetAsync($"http://localhost:{port}/health");
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return 1;
        }
    }
}
