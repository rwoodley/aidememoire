using Amazon.S3;
using Amazon.S3.Model;

namespace Persistence;

public class S3PairsRepository : IPairsRepository
{
    private const string S3BucketName = "aidememoire108";

    private readonly IAmazonS3 _s3Client;
    private readonly AudioGenerationService _audioGenerationService;

    public S3PairsRepository(IAmazonS3 s3Client, AudioGenerationService audioGenerationService)
    {
        _s3Client = s3Client;
        _audioGenerationService = audioGenerationService;
    }

    public async Task AddPairAsync(string bucketName, string prompt, string response)
    {
        var objectKey = $"1111/{bucketName}.csv";
        var existingContent = await GetExistingContentAsync(objectKey);

        var existingPairs = ParsePairsWithAudioIds(existingContent);

        string audioId;
        if (existingPairs.TryGetValue(prompt, out var existing))
        {
            audioId = existing.AudioId;
        }
        else
        {
            audioId = Guid.NewGuid().ToString();
        }

        existingPairs[prompt] = (Response: response, AudioId: audioId);

        var updatedContent = BuildCsvContent(existingPairs);
        await UploadContentAsync(objectKey, updatedContent);

        await _audioGenerationService.GenerateAudioAsync(bucketName, new List<(string, string)> { (audioId, response) });
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

    public async Task AppendCsvContentAsync(string bucketName, string csvContent)
    {
        var objectKey = $"1111/{bucketName}.csv";
        var existingContent = await GetExistingContentAsync(objectKey);

        var pairs = ParsePairsWithAudioIds(existingContent);
        var audioItems = new List<(string AudioId, string Text)>();

        foreach (var line in ParseCsvLines(csvContent))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var columns = ParseCsvColumns(line);
            if (columns.Count < 2)
                continue;

            var prompt = columns[0];
            var response = columns[1];

            if (pairs.TryGetValue(prompt, out var existing))
            {
                pairs[prompt] = (Response: response, AudioId: existing.AudioId);
                audioItems.Add((existing.AudioId, prompt));
            }
            else
            {
                var audioId = Guid.NewGuid().ToString();
                pairs[prompt] = (Response: response, AudioId: audioId);
                audioItems.Add((audioId, prompt));
            }
        }

        var updatedContent = BuildCsvContent(pairs);
        await UploadContentAsync(objectKey, updatedContent);

        if (audioItems.Count > 0)
        {
            await _audioGenerationService.GenerateAudioAsync(bucketName, audioItems);
        }
    }

    public async Task<Pair?> GetRandomPairAsync(string bucketName)
    {
        var objectKey = $"1111/{bucketName}.csv";
        var content = await GetExistingContentAsync(objectKey);

        if (string.IsNullOrEmpty(content))
            return null;

        var lines = ParseCsvLines(content)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (lines.Count == 0)
            return null;

        var randomIndex = Random.Shared.Next(lines.Count);
        var columns = ParseCsvColumns(lines[randomIndex]);

        if (columns.Count < 2)
            return null;

        var audioId = columns.Count >= 3 && !string.IsNullOrEmpty(columns[2])
            ? columns[2]
            : Guid.NewGuid().ToString();

        return new Pair(columns[0], columns[1], audioId);
    }

    public async Task<List<Pair>> GetAllPairsAsync(string bucketName)
    {
        var objectKey = $"1111/{bucketName}.csv";
        var content = await GetExistingContentAsync(objectKey);

        if (string.IsNullOrEmpty(content))
            return new List<Pair>();

        var pairs = new List<Pair>();
        foreach (var line in ParseCsvLines(content))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var columns = ParseCsvColumns(line);
            if (columns.Count < 2)
                continue;

            var audioId = columns.Count >= 3 && !string.IsNullOrEmpty(columns[2])
                ? columns[2]
                : Guid.NewGuid().ToString();

            pairs.Add(new Pair(columns[0], columns[1], audioId));
        }

        return pairs;
    }

    public async Task UpdatePairAsync(string bucketName, string oldPrompt, string newPrompt, string newResponse)
    {
        var objectKey = $"1111/{bucketName}.csv";
        var content = await GetExistingContentAsync(objectKey);
        var pairs = ParsePairsWithAudioIds(content);

        if (!pairs.TryGetValue(oldPrompt, out var existing))
            return;

        var oldAudioId = existing.AudioId;
        pairs.Remove(oldPrompt);

        string audioId;
        if (oldPrompt == newPrompt)
        {
            audioId = oldAudioId;
        }
        else
        {
            audioId = Guid.NewGuid().ToString();
        }

        pairs[newPrompt] = (Response: newResponse, AudioId: audioId);

        var updatedContent = BuildCsvContent(pairs);
        await UploadContentAsync(objectKey, updatedContent);

        if (oldPrompt != newPrompt)
        {
            await DeleteAudioFileAsync(bucketName, oldAudioId);
            await _audioGenerationService.GenerateAudioAsync(bucketName, new List<(string, string)> { (audioId, newPrompt) });
        }
    }

    public async Task DeletePairAsync(string bucketName, string prompt)
    {
        var objectKey = $"1111/{bucketName}.csv";
        var content = await GetExistingContentAsync(objectKey);

        if (string.IsNullOrEmpty(content))
            return;

        string? audioIdToDelete = null;
        var lines = new List<string>();

        foreach (var line in ParseCsvLines(content))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var columns = ParseCsvColumns(line);
            if (columns.Count < 2)
                continue;

            if (columns[0] == prompt)
            {
                if (columns.Count >= 3 && !string.IsNullOrEmpty(columns[2]))
                    audioIdToDelete = columns[2];
            }
            else
            {
                lines.Add(line);
            }
        }

        var updatedContent = string.Join(Environment.NewLine, lines);
        await UploadContentAsync(objectKey, updatedContent);

        if (audioIdToDelete != null)
        {
            await DeleteAudioFileAsync(bucketName, audioIdToDelete);
        }
    }

    public async Task CreateBucketAsync(string bucketName)
    {
        var objectKey = $"1111/{bucketName}.csv";
        var existing = await GetExistingContentAsync(objectKey);
        if (!string.IsNullOrEmpty(existing))
            return;

        await UploadContentAsync(objectKey, string.Empty);
    }

    public async Task<List<string>> ListBucketsAsync()
    {
        var request = new ListObjectsV2Request
        {
            BucketName = S3BucketName,
            Prefix = "1111/"
        };

        var response = await _s3Client.ListObjectsV2Async(request);
        return response.S3Objects
            .Where(o => o.Key.EndsWith(".csv"))
            .Select(o => Path.GetFileNameWithoutExtension(o.Key))
            .OrderBy(name => name)
            .ToList();
    }

    public async Task DeleteBucketAsync(string bucketName)
    {
        // Delete all audio files under this bucket
        var audioPrefix = $"1111/{bucketName}/";
        await DeleteAllObjectsWithPrefixAsync(audioPrefix);

        var request = new DeleteObjectRequest
        {
            BucketName = S3BucketName,
            Key = $"1111/{bucketName}.csv"
        };

        await _s3Client.DeleteObjectAsync(request);
    }

    public async Task RenameBucketAsync(string oldName, string newName)
    {
        var oldKey = $"1111/{oldName}.csv";
        var newKey = $"1111/{newName}.csv";

        await _s3Client.CopyObjectAsync(new CopyObjectRequest
        {
            SourceBucket = S3BucketName,
            SourceKey = oldKey,
            DestinationBucket = S3BucketName,
            DestinationKey = newKey
        });

        await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = S3BucketName,
            Key = oldKey
        });

        // Move all audio files from old to new bucket path
        var oldAudioPrefix = $"1111/{oldName}/";
        var listRequest = new ListObjectsV2Request
        {
            BucketName = S3BucketName,
            Prefix = oldAudioPrefix
        };

        var listResponse = await _s3Client.ListObjectsV2Async(listRequest);
        foreach (var obj in listResponse.S3Objects)
        {
            var fileName = Path.GetFileName(obj.Key);
            var newAudioKey = $"1111/{newName}/{fileName}";

            await _s3Client.CopyObjectAsync(new CopyObjectRequest
            {
                SourceBucket = S3BucketName,
                SourceKey = obj.Key,
                DestinationBucket = S3BucketName,
                DestinationKey = newAudioKey
            });

            await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = S3BucketName,
                Key = obj.Key
            });
        }
    }

    public async Task<string?> GetDefaultBucketAsync()
    {
        try
        {
            var request = new GetObjectRequest
            {
                BucketName = S3BucketName,
                Key = "1111/.default"
            };

            using var response = await _s3Client.GetObjectAsync(request);
            using var reader = new StreamReader(response.ResponseStream);
            var name = (await reader.ReadToEndAsync()).Trim();
            return string.IsNullOrEmpty(name) ? null : name;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task SetDefaultBucketAsync(string bucketName)
    {
        var request = new PutObjectRequest
        {
            BucketName = S3BucketName,
            Key = "1111/.default",
            ContentBody = bucketName,
            ContentType = "text/plain"
        };

        await _s3Client.PutObjectAsync(request);
    }

    public async Task<Stream?> GetAudioAsync(string bucketName, string audioId)
    {
        try
        {
            var request = new GetObjectRequest
            {
                BucketName = S3BucketName,
                Key = $"1111/{bucketName}/{audioId}.mp3"
            };

            var response = await _s3Client.GetObjectAsync(request);
            var memoryStream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;
            return memoryStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private async Task DeleteAudioFileAsync(string bucketName, string audioId)
    {
        var request = new DeleteObjectRequest
        {
            BucketName = S3BucketName,
            Key = $"1111/{bucketName}/{audioId}.mp3"
        };

        await _s3Client.DeleteObjectAsync(request);
    }

    private async Task DeleteAllObjectsWithPrefixAsync(string prefix)
    {
        var listRequest = new ListObjectsV2Request
        {
            BucketName = S3BucketName,
            Prefix = prefix
        };

        var listResponse = await _s3Client.ListObjectsV2Async(listRequest);
        foreach (var obj in listResponse.S3Objects)
        {
            await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = S3BucketName,
                Key = obj.Key
            });
        }
    }

    private Dictionary<string, (string Response, string AudioId)> ParsePairsWithAudioIds(string content)
    {
        var pairs = new Dictionary<string, (string Response, string AudioId)>();

        if (string.IsNullOrEmpty(content))
            return pairs;

        foreach (var line in ParseCsvLines(content))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var columns = ParseCsvColumns(line);
            if (columns.Count < 2)
                continue;

            var prompt = columns[0];
            var response = columns[1];
            var audioId = columns.Count >= 3 && !string.IsNullOrEmpty(columns[2])
                ? columns[2]
                : Guid.NewGuid().ToString();

            pairs[prompt] = (Response: response, AudioId: audioId);
        }

        return pairs;
    }

    private static string BuildCsvContent(Dictionary<string, (string Response, string AudioId)> pairs)
    {
        var lines = pairs.Select(kvp =>
            $"{EscapeCsvField(kvp.Key)},{EscapeCsvField(kvp.Value.Response)},{kvp.Value.AudioId}");
        return string.Join(Environment.NewLine, lines);
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

    private static List<string> ParseCsvLines(string content)
    {
        var lines = new List<string>();
        var currentLine = new System.Text.StringBuilder();
        var inQuotes = false;

        foreach (var ch in content)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                currentLine.Append(ch);
            }
            else if (ch == '\n' && !inQuotes)
            {
                lines.Add(currentLine.ToString().TrimEnd('\r'));
                currentLine.Clear();
            }
            else
            {
                currentLine.Append(ch);
            }
        }

        if (currentLine.Length > 0)
        {
            lines.Add(currentLine.ToString().TrimEnd('\r'));
        }

        return lines;
    }

    private static List<string> ParseCsvColumns(string line)
    {
        var columns = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];

            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == ',' && !inQuotes)
            {
                columns.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        columns.Add(current.ToString());
        return columns;
    }
}
