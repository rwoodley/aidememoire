namespace Persistence;

public interface IPairsRepository
{
    Task AddPairAsync(string bucketName, string prompt, string response);
    Task AppendCsvContentAsync(string bucketName, string csvContent);
    Task<Pair?> GetRandomPairAsync(string bucketName);
    Task<List<string>> ListBucketsAsync();
    Task DeleteBucketAsync(string bucketName);
    Task RenameBucketAsync(string oldName, string newName);
    Task<string?> GetDefaultBucketAsync();
    Task SetDefaultBucketAsync(string bucketName);
}
