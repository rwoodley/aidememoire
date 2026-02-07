using System.ComponentModel.DataAnnotations;

namespace Web.Api.Models;

public class UpdatePairRequest
{
    [Required]
    [MaxLength(1024)]
    public required string OldPrompt { get; init; }

    [Required]
    [MaxLength(1024)]
    public required string NewPrompt { get; init; }

    [Required]
    [MaxLength(1024)]
    public required string NewResponse { get; init; }
}
