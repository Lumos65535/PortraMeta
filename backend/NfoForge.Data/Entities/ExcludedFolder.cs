namespace NfoForge.Data.Entities;

public class ExcludedFolder
{
    public int Id { get; set; }
    public int LibraryId { get; set; }
    public Library Library { get; set; } = null!;

    /// <summary>Absolute path of the excluded subdirectory.</summary>
    public string Path { get; set; } = string.Empty;
}
