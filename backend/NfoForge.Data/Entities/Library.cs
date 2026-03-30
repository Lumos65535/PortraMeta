namespace NfoForge.Data.Entities;

public class Library
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<VideoFile> VideoFiles { get; set; } = [];
}
