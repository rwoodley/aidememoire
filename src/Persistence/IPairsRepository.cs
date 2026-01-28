namespace Persistence;

public interface IPairsRepository
{
    Task AddPairAsync(string bucketName, string prompt, string response);
}
