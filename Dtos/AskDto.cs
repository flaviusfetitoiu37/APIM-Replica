using System.ComponentModel.DataAnnotations;

namespace ApimReplica.Dtos;

public class AskDto
{
    [Required]
    public string Question { get; set; } = string.Empty;
}
