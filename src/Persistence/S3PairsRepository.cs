using Amazon.S3;
using Amazon.S3.Model;

namespace Persistence;

public class S3PairsRepository : IPairsRepository
{
    private const string S3BucketName = "aidememoire108";

    private readonly IAmazonS3 _s3Client;

    public S3PairsRepository(IAmazonS3 s3Client)
    {
        _s3Client = s3Client;
    }

    public async Task AddPairAsync(string bucketName, string prompt, string response)
    {
        var objectKey = $"aidememoire/1111/{bucketName}.csv";
        var existingContent = await GetExistingContentAsync(objectKey);
        var newRow = $"{EscapeCsvField(prompt)},{EscapeCsvField(response)}";
        var updatedContent = string.IsNullOrEmpty(existingContent)
            ? newRow
            : existingContent + Environment.NewLine + newRow;

        await UploadContentAsync(objectKey, updatedContent);
    }

    private async Task<string> GetExistingContentAsync(string objectKey)
    {
        try
        {
            var request = new GetObjectRequest
            {
                BucketName = S3BucketName,
                Key = objectKey
            };

            using var response = await _s3Client.GetObjectAsync(request);
            using var reader = new StreamReader(response.ResponseStream);
            return await reader.ReadToEndAsync();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return string.Empty;
        }
    }

    private async Task UploadContentAsync(string objectKey, string content)
    {
        var request = new PutObjectRequest
        {
            BucketName = S3BucketName,
            Key = objectKey,
            ContentBody = content,
            ContentType = "text/csv"
        };

        await _s3Client.PutObjectAsync(request);
    }

    private static string EscapeCsvField(string field)
    {
        if (field.Contains(',') || field.Contains('\n') || field.Contains('"'))
        {
            var escaped = field.Replace("\"", "\"\"");
            return $"\"{escaped}\"";
        }
        return field;
    }
}
