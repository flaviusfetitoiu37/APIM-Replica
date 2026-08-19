using System.ComponentModel.DataAnnotations.Schema;

namespace ApimReplica.Models;

public class Api
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string? SchemaUrl { get; set; }

    [Column(TypeName = "jsonb")]
    public string? Schema { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? HealthStatus { get; set; }
    public int? LastLatencyMs { get; set; }
    public DateTime? LastCheckedAt { get; set; }
    public string? HealthCheckUrl { get; set; }
}