namespace PortraMeta.Data.Entities;

public class Actor
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Aliases { get; set; } // JSON array
    public string? AvatarPath { get; set; }

    public ICollection<VideoActor> VideoActors { get; set; } = [];
}

public class VideoActor
{
    public int VideoFileId { get; set; }
    public VideoFile VideoFile { get; set; } = null!;

    public int ActorId { get; set; }
    public Actor Actor { get; set; } = null!;

    public string? Role { get; set; }
    public int Order { get; set; }
}
