namespace Persistence;

public interface IPairsRepository
{
    Task AddPairAsync(string bucketName, string prompt, string response);
    Task AppendCsvContentAsync(string bucketName, string csvContent);
    Task<Pair?> GetRandomPairAsync(string bucketName);
}
