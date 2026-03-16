namespace Wmux.Core.Models;

public sealed class Notification
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorkspaceId { get; init; }
    public Guid? SurfaceId { get; init; }
    public string Title { get; init; } = "";
    public string Body { get; init; } = "";
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
}
