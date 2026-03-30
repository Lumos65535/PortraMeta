using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using NfoForge.Core.Interfaces;

namespace NfoForge.Data.Services;

public class NfoParser(ILogger<NfoParser> logger) : INfoParser
{
    public async Task<NfoData?> ParseAsync(string nfoPath, CancellationToken ct = default)
    {
        if (!File.Exists(nfoPath))
            return null;

        try
        {
            var content = await File.ReadAllTextAsync(nfoPath, ct);
            var doc = XDocument.Parse(content);
            var root = doc.Root;

            if (root is null || root.Name.LocalName != "movie")
                return null;

            string? Get(string name) => root.Element(name)?.Value.Trim().NullIfEmpty();
            int? GetInt(string name) => int.TryParse(Get(name), out var v) ? v : null;

            var actors = root.Elements("actor")
                .Select((el, i) => new NfoActor(
                    el.Element("name")?.Value.Trim() ?? string.Empty,
                    el.Element("role")?.Value.Trim().NullIfEmpty(),
                    int.TryParse(el.Element("order")?.Value, out var ord) ? ord : i
                ))
                .Where(a => !string.IsNullOrEmpty(a.Name))
                .ToList();

            return new NfoData(
                Title: Get("title"),
                OriginalTitle: Get("originaltitle"),
                Year: GetInt("year"),
                Plot: Get("plot"),
                Studio: Get("studio"),
                Actors: actors
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse NFO: {Path}", nfoPath);
            return null;
        }
    }
}

file static class StringExtensions
{
    public static string? NullIfEmpty(this string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;
}
