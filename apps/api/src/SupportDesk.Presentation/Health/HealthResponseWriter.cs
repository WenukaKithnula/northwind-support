using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SupportDesk.Presentation.Health;

/// <summary>
/// Writes the health report as a small JSON document: the overall status and one word per check.
/// The reason a check failed is left out on purpose, because this endpoint is open to anyone and
/// an exception message can name servers, users or paths. ASP.NET logs a failing check on its own.
/// </summary>
public static class HealthResponseWriter
{
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var body = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(entry => entry.Key, entry => entry.Value.Status.ToString())
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonSerializerOptions.Web));
    }
}
