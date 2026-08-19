using System.ComponentModel.DataAnnotations.Schema;

namespace ApimReplica.Models;


public class ApiSchemaVersion
{
    public int Id { get; set; }
    public int ApiId { get; set; }
    public Api Api { get; set; } = null!;

    public int VersionNumber { get; set; }

    [Column(TypeName = "jsonb")]
    public string Content { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
}