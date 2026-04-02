using System.Xml.Linq;
using PortraMeta.Core.Interfaces;

namespace PortraMeta.Data.Services;

public class NfoService : INfoService
{
    public async Task WriteAsync(string nfoPath, VideoFileDto video, CancellationToken ct = default)
    {
        var root = new XElement("movie",
            new XElement("title", video.Title ?? string.Empty),
            new XElement("originaltitle", video.OriginalTitle ?? string.Empty),
            new XElement("year", video.Year?.ToString() ?? string.Empty),
            new XElement("plot", video.Plot ?? string.Empty),
            new XElement("studio", video.StudioName ?? string.Empty)
        );

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

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            root
        );

        await using var stream = File.Open(nfoPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await doc.SaveAsync(stream, SaveOptions.None, ct);
    }
}
