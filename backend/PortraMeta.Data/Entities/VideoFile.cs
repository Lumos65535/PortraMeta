namespace PortraMeta.Data.Entities;

public class VideoFile
{
    public int Id { get; set; }
    public int LibraryId { get; set; }
    public Library Library { get; set; } = null!;

    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }

    public bool HasNfo { get; set; }
    public bool HasPoster { get; set; }
    public bool HasFanart { get; set; }

    // NFO metadata (cached from NFO file)
    public string? Title { get; set; }
    public string? OriginalTitle { get; set; }
    public int? Year { get; set; }
    public string? Plot { get; set; }

    public int? StudioId { get; set; }
    public Studio? Studio { get; set; }

    // Tier 1 - extended NFO fields
    public string? DirectorsJson { get; set; }
    public string? GenresJson { get; set; }
    public int? Runtime { get; set; }
    public string? Mpaa { get; set; }
    public string? Premiered { get; set; }
    public string? RatingsJson { get; set; }
    public int? UserRating { get; set; }
    public string? UniqueIdsJson { get; set; }
    public string? TagsJson { get; set; }
    public string? SortTitle { get; set; }

    // Tier 2
    public string? Outline { get; set; }
    public string? Tagline { get; set; }
    public string? CreditsJson { get; set; }
    public string? CountriesJson { get; set; }

    // Tier 3
    public string? SetName { get; set; }
    public string? DateAdded { get; set; }
    public int? Top250 { get; set; }

    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
    public DateTime? NfoUpdatedAt { get; set; }
    public DateTime? FileModifiedAt { get; set; }

    public ICollection<VideoActor> VideoActors { get; set; } = [];
}
