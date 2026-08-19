using System.ComponentModel.DataAnnotations;

namespace ApimReplica.Dtos;

public class CreateRestApiDto
{
    [Required]
    [StringLength(64, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string BaseUrl { get; set; } = string.Empty;

    [Required]
    public string SchemaUrl { get; set; } = string.Empty;

    public string? HealthCheckUrl { get; set; }

}
