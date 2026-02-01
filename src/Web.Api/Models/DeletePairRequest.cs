using System.ComponentModel.DataAnnotations;

namespace Web.Api.Models;

public class DeletePairRequest
{
    [Required]
    [MaxLength(1024)]
    public required string Prompt { get; init; }
}
