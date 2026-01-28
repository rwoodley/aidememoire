using System.ComponentModel.DataAnnotations;

namespace Web.Api.Models;

public class AddPairRequest
{
    [Required]
    [MaxLength(64)]
    [RegularExpression(@"^[a-zA-Z0-9][a-zA-Z0-9\-]*[a-zA-Z0-9]$|^[a-zA-Z0-9]$",
        ErrorMessage = "BucketName must contain only alphanumeric characters and hyphens, and cannot start or end with a hyphen.")]
    public required string BucketName { get; init; }

    [Required]
    [MaxLength(1024)]
    public required string Prompt { get; init; }

    [Required]
    [MaxLength(1024)]
    public required string Response { get; init; }
}
