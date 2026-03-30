namespace NfoForge.Data.Entities;

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

    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
    public DateTime? NfoUpdatedAt { get; set; }

    public ICollection<VideoActor> VideoActors { get; set; } = [];
}
