using System.ComponentModel.DataAnnotations;

namespace Web.Api.Models;

public class CreateBucketRequest
{
    [Required]
    [MaxLength(64)]
    [RegularExpression(@"^[a-zA-Z0-9][a-zA-Z0-9\-]*[a-zA-Z0-9]$|^[a-zA-Z0-9]$",
        ErrorMessage = "BucketName must contain only alphanumeric characters and hyphens, and cannot start or end with a hyphen.")]
    public required string BucketName { get; init; }
}
