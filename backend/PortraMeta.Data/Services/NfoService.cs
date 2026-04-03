using System.Xml.Linq;
using PortraMeta.Core.Interfaces;

namespace PortraMeta.Data.Services;

public class NfoService : INfoService
{
    // Standard Kodi element ordering for clean XML output
    private static readonly string[] KnownElements =
    [
        "title", "originaltitle", "sorttitle", "ratings", "userrating", "top250",
        "outline", "plot", "tagline", "runtime", "mpaa", "uniqueid", "genre",
        "country", "credits", "director", "premiered", "year", "studio", "tag",
        "set", "actor", "dateadded"
    ];

    public async Task WriteAsync(string nfoPath, VideoFileDto video, CancellationToken ct = default)
    {
        XElement root;

        // Round-trip: load existing NFO to preserve unknown elements
        if (File.Exists(nfoPath))
        {
            try
            {
                var existing = await File.ReadAllTextAsync(nfoPath, ct);
                var existingDoc = XDocument.Parse(existing);
                root = existingDoc.Root ?? new XElement("movie");
                if (root.Name.LocalName != "movie")
                    root = new XElement("movie");
            }
            catch
            {
                root = new XElement("movie");
            }
        }
        else
        {
            root = new XElement("movie");
        }

        // Helper to set a single-value element
        void SetElement(string name, string? value)
        {
            root.Elements(name).Remove();
            if (value is not null)
                root.Add(new XElement(name, value));
            else
                root.Add(new XElement(name, string.Empty));
        }

        // Helper to set multi-value elements
        void SetMultiple(string name, IReadOnlyList<string>? values)
        {
            root.Elements(name).Remove();
            if (values is not null)
            {
                foreach (var v in values)
                    root.Add(new XElement(name, v));
            }
        }

        // Basic fields
        SetElement("title", video.Title);
        SetElement("originaltitle", video.OriginalTitle);
        SetElement("sorttitle", video.SortTitle);
        SetElement("year", video.Year?.ToString());
        SetElement("plot", video.Plot);
        SetElement("studio", video.StudioName);

        // Tier 1
        SetMultiple("director", video.Directors);
        SetMultiple("genre", video.Genres);
        SetElement("runtime", video.Runtime?.ToString());
        SetElement("mpaa", video.Mpaa);
        SetElement("premiered", video.Premiered);
        SetElement("userrating", video.UserRating?.ToString());
        SetMultiple("tag", video.Tags);

        // Ratings
        root.Elements("ratings").Remove();
        root.Elements("rating").Remove(); // remove legacy single rating too
        if (video.Ratings is { Count: > 0 })
        {
            var ratingsEl = new XElement("ratings");
            foreach (var r in video.Ratings)
            {
                ratingsEl.Add(new XElement("rating",
                    new XAttribute("name", r.Name),
                    new XAttribute("max", r.Max),
                    new XElement("value", r.Value),
                    new XElement("votes", r.Votes)
                ));
            }
            root.Add(ratingsEl);
        }

        // UniqueIds
        root.Elements("uniqueid").Remove();
        root.Elements("imdbid").Remove(); // remove legacy
        if (video.UniqueIds is { Count: > 0 })
        {
            foreach (var uid in video.UniqueIds)
                root.Add(new XElement("uniqueid", new XAttribute("type", uid.Key), uid.Value));
        }

        // Tier 2
        SetElement("outline", video.Outline);
        SetElement("tagline", video.Tagline);
        SetMultiple("credits", video.Credits);
        SetMultiple("country", video.Countries);

        // Tier 3 - Set
        root.Elements("set").Remove();
        if (!string.IsNullOrEmpty(video.SetName))
            root.Add(new XElement("set", new XElement("name", video.SetName)));

        SetElement("dateadded", video.DateAdded);
        SetElement("top250", video.Top250?.ToString());

        // Actors
        root.Elements("actor").Remove();
        if (video.Actors is not null)
        {
            foreach (var actor in video.Actors.OrderBy(a => a.Order))
            {
                root.Add(new XElement("actor",
                    new XElement("name", actor.Name),
                    new XElement("role", actor.Role ?? string.Empty),
                    new XElement("order", actor.Order)
                ));
            }
        }

        // Reorder elements: known elements first in standard order, then unknown elements
        var knownSet = new HashSet<string>(KnownElements);
        var unknownElements = root.Elements()
            .Where(e => !knownSet.Contains(e.Name.LocalName))
            .ToList();

        var orderedElements = new List<XElement>();
        foreach (var name in KnownElements)
        {
            orderedElements.AddRange(root.Elements(name));
        }
        orderedElements.AddRange(unknownElements);

        root.RemoveNodes();
        foreach (var el in orderedElements)
            root.Add(el);

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            root
        );

        await using var stream = File.Open(nfoPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await doc.SaveAsync(stream, SaveOptions.None, ct);
    }
}
