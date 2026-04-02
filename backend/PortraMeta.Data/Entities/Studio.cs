namespace PortraMeta.Data.Entities;

public class Studio
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? LogoPath { get; set; }

    public ICollection<VideoFile> VideoFiles { get; set; } = [];
}
