using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using PortraMeta.Core.Interfaces;

namespace PortraMeta.Data.Services;

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

            List<string> GetMultiple(string name) => root.Elements(name)
                .Select(e => e.Value.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            var actors = root.Elements("actor")
                .Select((el, i) => new NfoActor(
                    el.Element("name")?.Value.Trim() ?? string.Empty,
                    el.Element("role")?.Value.Trim().NullIfEmpty(),
                    int.TryParse(el.Element("order")?.Value, out var ord) ? ord : i
                ))
                .Where(a => !string.IsNullOrEmpty(a.Name))
                .ToList();

            // Parse <ratings><rating name="" max=""><value/><votes/></rating></ratings>
            var ratings = new List<NfoRating>();
            var ratingsEl = root.Element("ratings");
            if (ratingsEl is not null)
            {
                foreach (var r in ratingsEl.Elements("rating"))
                {
                    var name = r.Attribute("name")?.Value ?? "default";
                    var max = int.TryParse(r.Attribute("max")?.Value, out var m) ? m : 10;
                    if (decimal.TryParse(r.Element("value")?.Value, out var val))
                    {
                        int.TryParse(r.Element("votes")?.Value, out var votes);
                        ratings.Add(new NfoRating(name, val, votes, max));
                    }
                }
            }
            // Also support legacy <rating>8.5</rating> as a single rating
            if (ratings.Count == 0 && decimal.TryParse(Get("rating"), out var legacyRating))
            {
                ratings.Add(new NfoRating("default", legacyRating, 0));
            }

            // Parse <uniqueid type="imdb">tt1234567</uniqueid>
            var uniqueIds = root.Elements("uniqueid")
                .Select(e => new NfoUniqueId(
                    e.Attribute("type")?.Value ?? "unknown",
                    e.Value.Trim()
                ))
                .Where(u => !string.IsNullOrEmpty(u.Value))
                .ToList();

            // Also support legacy <imdbid> element
            var legacyImdb = Get("imdbid");
            if (legacyImdb is not null && !uniqueIds.Any(u => u.Type == "imdb"))
                uniqueIds.Add(new NfoUniqueId("imdb", legacyImdb));

            // Parse <set><name>...</name></set> or plain <set>...</set>
            string? set = null;
            var setEl = root.Element("set");
            if (setEl is not null)
            {
                var setName = setEl.Element("name")?.Value.Trim().NullIfEmpty();
                set = setName ?? setEl.Value.Trim().NullIfEmpty();
            }

            return new NfoData(
                Title: Get("title"),
                OriginalTitle: Get("originaltitle"),
                Year: GetInt("year"),
                Plot: Get("plot"),
                Studio: Get("studio"),
                Actors: actors,
                Directors: GetMultiple("director"),
                Genres: GetMultiple("genre"),
                Runtime: GetInt("runtime"),
                Mpaa: Get("mpaa"),
                Premiered: Get("premiered"),
                Ratings: ratings,
                UserRating: GetInt("userrating"),
                UniqueIds: uniqueIds,
                Tags: GetMultiple("tag"),
                SortTitle: Get("sorttitle"),
                Outline: Get("outline"),
                Tagline: Get("tagline"),
                Credits: GetMultiple("credits"),
                Countries: GetMultiple("country"),
                Set: set,
                DateAdded: Get("dateadded"),
                Top250: GetInt("top250")
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
